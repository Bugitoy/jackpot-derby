using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides small street props and parked cars when they are far from the camera. Out there they are a
/// few pixels big but each one still costs a draw call, and draw calls are expensive in WebGL.
/// Buildings, roads and anything large always stay. Unity's CullingGroup does the distance checks
/// (no per-object scripts); props that can be pushed (parked cars) get their positions refreshed.
/// Paused while the title screen orbits high over the city. Distances scale with GraphicsQuality.
/// </summary>
public class DetailCuller : MonoBehaviour
{
    [Tooltip("Props no bigger than this (largest side, units) count as small.")]
    [SerializeField] float smallSize = 3.6f;
    [Tooltip("Props no bigger than this count as medium (vehicles, trees, kiosks); anything larger always shows.")]
    [SerializeField] float mediumSize = 11f;
    [Tooltip("Small props hide beyond this distance from the camera.")]
    [SerializeField] float smallDistance = 85f;
    [Tooltip("Medium props hide beyond this distance from the camera.")]
    [SerializeField] float mediumDistance = 140f;

    public static DetailCuller Instance { get; private set; }

    class Group
    {
        public CullingGroup cull;
        public BoundingSphere[] spheres;
        public readonly List<Renderer[]> renderers = new List<Renderer[]>();
        public readonly List<(int index, Transform t, Vector3 offset)> movers = new List<(int, Transform, Vector3)>();
        public float baseDistance;
    }

    readonly Group[] groups = new Group[2];
    Camera cam;
    float distanceScale = 1f;
    bool suspended;
    bool needsSync = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake() => Instance = this;

    void Start()
    {
        cam = GetComponent<Camera>();
        var arena = FindAnyObjectByType<ArenaGrid>();
        if (cam == null || arena == null) { enabled = false; return; }
        groups[0] = new Group { baseDistance = smallDistance };
        groups[1] = new Group { baseDistance = mediumDistance };
        var spheres = new[] { new List<BoundingSphere>(), new List<BoundingSphere>() };

        // One item per scenery object: a renderer whose parent has no renderer (child renderers such
        // as a parked car's wheels belong to it).
        foreach (var r in arena.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.isPartOfStaticBatch) continue;
            var p = r.transform.parent;
            if (p != null && p.GetComponentInParent<MeshRenderer>() != null) continue;
            var rs = r.GetComponentsInChildren<Renderer>();
            Bounds b = r.bounds;
            foreach (var c in rs) b.Encapsulate(c.bounds);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            int g = size <= smallSize ? 0 : size <= mediumSize ? 1 : -1;
            if (g < 0) continue;
            var group = groups[g];
            int index = spheres[g].Count;
            spheres[g].Add(new BoundingSphere(b.center, b.extents.magnitude));
            group.renderers.Add(rs);
            if (r.GetComponentInParent<Rigidbody>() != null)
                group.movers.Add((index, r.transform, Quaternion.Inverse(r.transform.rotation) * (b.center - r.transform.position)));
        }

        foreach (var (group, list) in new[] { (groups[0], spheres[0]), (groups[1], spheres[1]) })
        {
            group.spheres = list.ToArray();
            group.cull = new CullingGroup { targetCamera = cam };
            group.cull.SetBoundingSpheres(group.spheres);
            group.cull.SetBoundingSphereCount(group.spheres.Length);
            group.cull.SetDistanceReferencePoint(cam.transform);
            var g = group;
            group.cull.onStateChanged = ev =>
            {
                if (ev.hasBecomeVisible || ev.hasBecomeInvisible || ev.currentDistance != ev.previousDistance) Apply(g, ev.index);
            };
        }
        SetDistanceScale(distanceScale);
    }

    /// <summary>Scale both hide distances (GraphicsQuality: 1 on High, less on Low).</summary>
    public void SetDistanceScale(float scale)
    {
        distanceScale = scale;
        foreach (var g in groups)
            if (g?.cull != null) g.cull.SetBoundingDistances(new[] { g.baseDistance * scale });
        // The new bands are measured on the next culling pass; their state-change events update the
        // renderers then (syncing now would read the old distances).
    }

    void Apply(Group g, int i)
    {
        bool show = suspended || g.cull.GetDistance(i) == 0;
        foreach (var r in g.renderers[i])
            if (r != null) r.forceRenderingOff = !show;
    }

    void LateUpdate()
    {
        bool title = Menus.Instance != null && Menus.Instance.ShowingTitle;
        if (title != suspended) { suspended = title; needsSync = true; }
        foreach (var g in groups)
        {
            if (g?.cull == null) continue;
            foreach (var (index, t, offset) in g.movers)
                if (t != null) g.spheres[index].position = t.position + t.rotation * offset;
        }
        if (!needsSync) return;
        needsSync = false;
        foreach (var g in groups)
            if (g?.cull != null)
                for (int i = 0; i < g.spheres.Length; i++) Apply(g, i);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var g in groups)
        {
            if (g?.cull == null) continue;
            g.cull.Dispose();
            g.cull = null;
        }
    }
}
