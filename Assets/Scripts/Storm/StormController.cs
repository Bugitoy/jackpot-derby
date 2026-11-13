using UnityEngine;

/// <summary>
/// The Bad Luck Storm. The safe zone starts as the whole arena; from 60 s it shrinks every 30 s toward a
/// point near the centre, down to about 30×20 units by ~4 minutes. Outside it: 5 damage per second,
/// dark purple clouds and walls. Other systems ask IsInside / ClosestSafePoint.
/// </summary>
public class StormController : MonoBehaviour
{
    public static StormController Instance { get; private set; }

    [SerializeField] ArenaGrid grid;
    [SerializeField] float firstShrinkAt = 60f;
    [SerializeField] float stageInterval = 30f;
    [SerializeField] int stages = 6;
    [Tooltip("How long each shrink takes (the zone then holds until the next stage).")]
    [SerializeField] float shrinkDuration = 12f;
    [SerializeField] Vector2 finalSize = new Vector2(30f, 20f);
    [Tooltip("The final zone is centred up to this far from the middle of the arena.")]
    [SerializeField] float centreJitter = 14f;
    [SerializeField] float damagePerSecond = 5f;

    [Header("Look")]
    [SerializeField] Material wallMaterial;
    [SerializeField] Material groundMaterial;
    [SerializeField] Material cloudMaterial;
    [SerializeField] float wallHeight = 14f;

    public Rect Zone { get; private set; }
    /// <summary>The zone at the end of the current/next shrink (where it's heading).</summary>
    public Rect TargetZone { get; private set; }
    public bool IsShrinking { get; private set; }
    /// <summary>Seconds until the next shrink starts (0 while shrinking, -1 when finished).</summary>
    public float TimeToNextShrink { get; private set; }

    Rect fullZone, finalZone;
    Transform[] walls = new Transform[4];
    Transform[] darkGround = new Transform[4];
    ParticleSystem[] clouds = new ParticleSystem[4];
    float damageTick;

    void Awake()
    {
        Instance = this;
        if (grid == null) grid = FindAnyObjectByType<ArenaGrid>();
        Vector2 size = grid != null ? grid.SizeInUnits : new Vector2(204f, 132f);
        fullZone = new Rect(-size.x / 2f, -size.y / 2f, size.x, size.y);
        Vector2 c = new Vector2(Random.Range(-centreJitter, centreJitter), Random.Range(-centreJitter * 0.6f, centreJitter * 0.6f));
        finalZone = new Rect(c.x - finalSize.x / 2f, c.y - finalSize.y / 2f, finalSize.x, finalSize.y);
        Zone = TargetZone = fullZone;
        BuildVisuals();
        ApplyVisuals();
    }

    void Update()
    {
        var mm = MatchManager.Instance;
        float t = mm != null ? mm.MatchTime : Time.timeSinceLevelLoad;
        bool playing = mm == null || mm.State == MatchState.Playing;

        // Which stage are we in, and how far through its shrink?
        float sinceFirst = t - firstShrinkAt;
        int stage = sinceFirst < 0f ? 0 : Mathf.Min(stages, Mathf.FloorToInt(sinceFirst / stageInterval) + 1);
        float inStage = sinceFirst < 0f ? 0f : sinceFirst - (stage - 1) * stageInterval;
        float k = stage == 0 ? 0f : Mathf.Clamp01(inStage / shrinkDuration);
        float from = Mathf.Max(0, stage - 1) / (float)stages, to = stage / (float)stages;
        Zone = Lerp(fullZone, finalZone, Mathf.Lerp(from, to, Smooth(k)));
        IsShrinking = stage > 0 && k < 1f && stage <= stages;

        int nextStage = stage < stages ? stage + 1 : stages;
        TargetZone = IsShrinking ? Lerp(fullZone, finalZone, to) : Lerp(fullZone, finalZone, nextStage / (float)stages);
        if (stage >= stages && !IsShrinking) TimeToNextShrink = -1f;
        else if (IsShrinking) TimeToNextShrink = 0f;
        else TimeToNextShrink = sinceFirst < 0f ? -sinceFirst : stageInterval - inStage;

        ApplyVisuals();

        if (!playing) return;
        damageTick += Time.deltaTime;
        if (damageTick >= 0.5f)
        {
            damageTick -= 0.5f;
            foreach (var car in CarHealth.Alive.ToArray())
                if (!IsInside(car.transform.position, 0f))
                    car.TakeDamage(damagePerSecond * 0.5f, null, DamageKind.Storm, car.transform.position + Vector3.up * 2f);
        }
    }

    static float Smooth(float x) => x * x * (3f - 2f * x);

    static Rect Lerp(Rect a, Rect b, float t) =>
        Rect.MinMaxRect(Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t), Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));

    /// <summary>Is the point inside the current safe zone, at least `margin` from its edge?</summary>
    public bool IsInside(Vector3 p, float margin) =>
        p.x > Zone.xMin + margin && p.x < Zone.xMax - margin && p.z > Zone.yMin + margin && p.z < Zone.yMax - margin;

    /// <summary>Inside where the zone is heading (safe for a while).</summary>
    public bool IsInsideTarget(Vector3 p, float margin) =>
        p.x > TargetZone.xMin + margin && p.x < TargetZone.xMax - margin && p.z > TargetZone.yMin + margin && p.z < TargetZone.yMax - margin;

    public Vector3 SafeCentre => new Vector3(TargetZone.center.x, 0f, TargetZone.center.y);

    /// <summary>Nearest point a little inside the target zone.</summary>
    public Vector3 ClosestSafePoint(Vector3 p, float margin = 6f)
    {
        var r = TargetZone;
        float mx = Mathf.Min(margin, r.width * 0.4f), mz = Mathf.Min(margin, r.height * 0.4f);
        return new Vector3(Mathf.Clamp(p.x, r.xMin + mx, r.xMax - mx), 0f, Mathf.Clamp(p.z, r.yMin + mz, r.yMax - mz));
    }

    // ---------------- visuals ----------------

    void BuildVisuals()
    {
        for (int i = 0; i < 4; i++)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "StormWall" + i;
            Destroy(w.GetComponent<Collider>());
            w.transform.SetParent(transform, false);
            var wr = w.GetComponent<Renderer>();
            wr.sharedMaterial = wallMaterial;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            walls[i] = w.transform;

            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = "StormGround" + i;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var gr = g.GetComponent<Renderer>();
            gr.sharedMaterial = groundMaterial;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            darkGround[i] = g.transform;

            clouds[i] = MakeClouds(i);
        }
    }

    ParticleSystem MakeClouds(int i)
    {
        var go = new GameObject("StormClouds" + i);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.12f, 0.3f, 0.75f), new Color(0.12f, 0.08f, 0.16f, 0.85f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;
        var em = ps.emission;
        em.rateOverTime = 20f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = cloudMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    void ApplyVisuals()
    {
        var z = Zone;
        float far = 400f;
        // 0 north, 1 south, 2 east, 3 west.
        SetWall(0, new Vector3(z.center.x, 0f, z.yMax), new Vector3(z.width, 0f, 0f));
        SetWall(1, new Vector3(z.center.x, 0f, z.yMin), new Vector3(z.width, 0f, 0f));
        SetWall(2, new Vector3(z.xMax, 0f, z.center.y), new Vector3(0f, 0f, z.height));
        SetWall(3, new Vector3(z.xMin, 0f, z.center.y), new Vector3(0f, 0f, z.height));

        // Dark ground outside: north/south strips span the full width, east/west fill the sides.
        SetGround(0, new Vector3(0f, 0.4f, z.yMax + far / 2f), new Vector2(far * 2f, far));
        SetGround(1, new Vector3(0f, 0.4f, z.yMin - far / 2f), new Vector2(far * 2f, far));
        SetGround(2, new Vector3(z.xMax + far / 2f, 0.4f, z.center.y), new Vector2(far, z.height));
        SetGround(3, new Vector3(z.xMin - far / 2f, 0.4f, z.center.y), new Vector2(far, z.height));
    }

    void SetWall(int i, Vector3 centre, Vector3 along)
    {
        float len = Mathf.Max(along.x, along.z);
        bool alongX = along.x > 0f;
        walls[i].position = centre + Vector3.up * wallHeight / 2f;
        walls[i].localScale = alongX ? new Vector3(len, wallHeight, 0.4f) : new Vector3(0.4f, wallHeight, len);
        var shape = clouds[i].shape;
        shape.position = Vector3.zero;
        clouds[i].transform.position = centre + Vector3.up * 3f;
        shape.scale = alongX ? new Vector3(len, 4f, 6f) : new Vector3(6f, 4f, len);
        var em = clouds[i].emission;
        em.rateOverTime = Mathf.Clamp(len * 0.15f, 3f, 25f);
    }

    void SetGround(int i, Vector3 centre, Vector2 size)
    {
        darkGround[i].position = centre;
        darkGround[i].localScale = new Vector3(size.x, size.y, 1f);
    }
}
