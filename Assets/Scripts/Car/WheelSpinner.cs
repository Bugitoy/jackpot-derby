using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spins the car model's wheels with its speed, and turns the front pair with the steering.
/// The Tripo car models have each wheel (tyre and rim) as separate parts, so at start-up this finds
/// them by shape: thin, round parts low down at the sides of the body. Each wheel's parts go under a
/// pivot at the wheel's centre, which is then rotated. The Tripo wheels aren't fully modelled (gaps
/// show when they spin), so they are hidden and Lefika's wheel model is fitted in their place.
/// Runs before CarFx so the fitted wheels are included in its hit flashes.
/// </summary>
[DefaultExecutionOrder(-100)]
public class WheelSpinner : MonoBehaviour
{
    [Tooltip("Where to look for wheel parts (the car's model).")]
    [SerializeField] Transform model;
    [Tooltip("Wheel model fitted in place of the car model's own wheels (axle along its Z). Empty = keep the originals.")]
    [SerializeField] GameObject wheelPrefab;
    [Tooltip("Front wheel turn at full steering, degrees.")]
    [SerializeField] float maxSteerAngle = 24f;
    [SerializeField] float steerSmoothing = 10f;

    class Wheel { public Transform pivot; public Quaternion rest; public float radius, angle; public bool front; }

    readonly List<Wheel> wheels = new List<Wheel>();
    Rigidbody body;
    CarInputSource input;
    float steer;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        input = GetComponent<CarInputSource>();
        if (model == null) { var v = transform.Find("Visual/Model"); model = v != null ? v : transform; }
        FindWheels();
    }

    void FindWheels()
    {
        // Measure every model part in the car's own space.
        var parts = new List<(Renderer r, Bounds b)>();
        Bounds car = new Bounds();
        bool any = false;
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount < 24) continue;
            Bounds lb = LocalBounds(mf);
            parts.Add((r, lb));
            if (!any) { car = lb; any = true; } else car.Encapsulate(lb);
        }
        if (!any) return;

        // A wheel part is round in the side view (Y ≈ Z), thin across the car (X), sits in the lower
        // part of the body and out towards one side.
        var candidates = new List<(Renderer r, Bounds b)>();
        foreach (var p in parts)
        {
            Vector3 s = p.b.size;
            float d = Mathf.Max(s.y, s.z);
            bool round = Mathf.Abs(s.y - s.z) < d * 0.2f;
            bool thin = s.x < d * 0.75f;
            bool sized = d > car.size.z * 0.12f && d < car.size.z * 0.4f;
            bool low = p.b.center.y < car.min.y + car.size.y * 0.42f;
            bool side = Mathf.Abs(p.b.center.x - car.center.x) > car.size.x * 0.18f;
            if (round && thin && sized && low && side) candidates.Add(p);
        }

        // Group tyres and rims that share a centre into one wheel.
        var groups = new List<List<(Renderer r, Bounds b)>>();
        foreach (var c in candidates)
        {
            List<(Renderer r, Bounds b)> home = null;
            foreach (var g in groups)
                if (Vector3.Distance(g[0].b.center, c.b.center) < Mathf.Max(g[0].b.size.y, c.b.size.y) * 0.35f) { home = g; break; }
            if (home == null) groups.Add(home = new List<(Renderer r, Bounds b)>());
            home.Add(c);
        }

        foreach (var g in groups)
        {
            Bounds wb = g[0].b;
            foreach (var p in g) wb.Encapsulate(p.b);
            // Pivots live under the model so CarFx (which collects the model's renderers) flashes them.
            var pivot = new GameObject("WheelPivot").transform;
            pivot.SetParent(model, false);
            pivot.position = transform.TransformPoint(wb.center);
            pivot.rotation = transform.rotation;
            bool right = wb.center.x > car.center.x;
            if (wheelPrefab != null && FitWheel(pivot, Mathf.Max(wb.size.y, wb.size.z), right))
                foreach (var p in g) p.r.gameObject.SetActive(false);
            else
                foreach (var p in g) p.r.transform.SetParent(pivot, true);
            wheels.Add(new Wheel { pivot = pivot, rest = pivot.localRotation, radius = Mathf.Max(0.1f, Mathf.Max(wb.size.y, wb.size.z) * 0.5f), front = wb.center.z > car.center.z });
        }
    }

    /// <summary>
    /// Put a copy of the wheel model on a pivot, centred, sized to the original wheel's diameter,
    /// with its axle along the car's X and its outer face pointing away from the car.
    /// </summary>
    bool FitWheel(Transform pivot, float diameter, bool rightSide)
    {
        var mf = wheelPrefab.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        Bounds mb = mf.sharedMesh.bounds;
        float meshDiameter = Mathf.Max(mb.size.x, mb.size.y);
        if (meshDiameter <= 0f) return false;

        var w = Instantiate(wheelPrefab, pivot);
        w.name = "Wheel";
        foreach (var c in w.GetComponentsInChildren<Collider>()) Destroy(c);
        w.transform.localRotation = Quaternion.Euler(0f, rightSide ? 90f : -90f, 0f);
        float k = diameter / meshDiameter / Mathf.Max(1e-4f, pivot.lossyScale.x);
        w.transform.localScale = Vector3.one * k;
        w.transform.localPosition = -(w.transform.localRotation * (mb.center * k));
        return true;
    }

    /// <summary>A mesh's bounds in this car's local space.</summary>
    Bounds LocalBounds(MeshFilter mf)
    {
        Bounds mb = mf.sharedMesh.bounds;
        Matrix4x4 m = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        Bounds b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            b.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return b;
    }

    void LateUpdate()
    {
        if (wheels.Count == 0) return;
        float dt = Time.deltaTime;
        float forward = body != null ? Vector3.Dot(body.linearVelocity, transform.forward) : 0f;
        float targetSteer = input != null && input.enabled ? Mathf.Clamp(input.Steer, -1f, 1f) * maxSteerAngle : 0f;
        steer = Mathf.Lerp(steer, targetSteer, 1f - Mathf.Exp(-steerSmoothing * dt));
        foreach (var w in wheels)
        {
            // Rolling forward turns the wheel about the car's +X axis (its top moves forward).
            w.angle = (w.angle + forward * dt / w.radius * Mathf.Rad2Deg) % 360f;
            Quaternion turn = w.front ? Quaternion.Euler(0f, steer, 0f) : Quaternion.identity;
            w.pivot.localRotation = w.rest * turn * Quaternion.AngleAxis(w.angle, Vector3.right);
        }
    }
}
