using UnityEngine;

/// <summary>
/// All the juicy one-shot effects, made from a few shared particle systems built in code:
/// hit puffs, sparks, coin bursts, explosions, blast rings, plus looping smoke and turbo speed lines.
/// </summary>
public class FxLibrary : MonoBehaviour
{
    public static FxLibrary I { get; private set; }

    [Tooltip("Additive soft glow (sparks, flashes, speed lines).")]
    [SerializeField] Material glowMaterial;
    [Tooltip("Alpha-blended soft puff (smoke, dust).")]
    [SerializeField] Material puffMaterial;
    [Tooltip("Lit gold material for coin particles.")]
    [SerializeField] Material coinMaterial;
    [SerializeField] Mesh coinMesh;
    [Tooltip("Additive ring texture material for blast shockwaves.")]
    [SerializeField] Material ringMaterial;

    ParticleSystem puffs, sparks, coins, rings, flashes;
    Transform groundPlane;

    void Awake()
    {
        I = this;
        groundPlane = new GameObject("FxGroundPlane").transform;
        groundPlane.SetParent(transform, false);
        groundPlane.position = Vector3.zero;

        puffs = MakeSystem("Puffs", puffMaterial, 400, 0f);
        sparks = MakeSystem("Sparks", glowMaterial, 400, 1.5f);
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.06f;
        sr.lengthScale = 1.5f;
        flashes = MakeSystem("Flashes", glowMaterial, 60, 0f);

        coins = MakeSystem("Coins", coinMaterial, 300, 2.2f);
        var cr = coins.GetComponent<ParticleSystemRenderer>();
        cr.renderMode = ParticleSystemRenderMode.Mesh;
        cr.mesh = coinMesh;
        cr.alignment = ParticleSystemRenderSpace.World;
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var main = coins.main;
        main.startRotation3D = true;
        var rot = coins.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        var col = coins.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.Planes;
        col.SetPlane(0, groundPlane);
        col.bounce = 0.45f;
        col.dampen = 0.35f;

        rings = MakeSystem("Rings", ringMaterial != null ? ringMaterial : glowMaterial, 20, 0f);
        var rr = rings.GetComponent<ParticleSystemRenderer>();
        rr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        var size = rings.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));

        // Puffs, sparks and flashes shrink/fade out over their life.
        foreach (var ps in new[] { puffs, sparks, flashes })
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
        }
        foreach (var ps in new[] { puffs, sparks, flashes, rings, coins })
        {
            var colr = ps.colorOverLifetime;
            colr.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colr.color = g;
        }
    }

    ParticleSystem MakeSystem(string name, Material mat, int max, float gravity)
    {
        var go = new GameObject("Fx_" + name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.startSpeed = 0f;
        var em = ps.emission;
        em.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    void Emit(ParticleSystem ps, Vector3 pos, Vector3 velocity, float size, float life, Color color)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = velocity,
            startSize = size,
            startLifetime = life,
            startColor = color,
            applyShapeToPosition = false
        };
        ps.Emit(p, 1);
    }

    // ---------- public effects ----------

    /// <summary>Chunky "pop" where a bolt lands.</summary>
    public void HitPuff(Vector3 pos, Color color, float scale = 1f)
    {
        Emit(flashes, pos, Vector3.zero, 2.6f * scale, 0.14f, Color.white);
        Emit(flashes, pos, Vector3.zero, 3.4f * scale, 0.22f, color);
        for (int i = 0; i < 7; i++)
            Emit(sparks, pos, Random.onUnitSphere * Random.Range(6f, 12f) + Vector3.up * 3f, Random.Range(0.25f, 0.45f) * scale, Random.Range(0.25f, 0.45f), color);
        for (int i = 0; i < 3; i++)
            Emit(puffs, pos + Random.insideUnitSphere * 0.4f, Random.insideUnitSphere * 1.5f + Vector3.up, Random.Range(1.0f, 1.6f) * scale, Random.Range(0.35f, 0.55f), new Color(1f, 1f, 1f, 0.7f));
    }

    public void MuzzleFlash(Vector3 pos, Color color, float scale = 1f)
    {
        Emit(flashes, pos, Vector3.zero, 1.4f * scale, 0.08f, color);
    }

    public void CoinBurst(Vector3 pos, int count)
    {
        var gold = new Color(1f, 0.85f, 0.3f);
        for (int i = 0; i < count; i++)
        {
            Vector3 v = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * Random.Range(3f, 9f) + Vector3.up * Random.Range(8f, 15f);
            Emit(coins, pos + Vector3.up * 1.5f, v, Random.Range(0.45f, 0.68f), Random.Range(1.6f, 2.4f), gold);
        }
    }

    public void Explosion(Vector3 pos, float scale = 1f)
    {
        Emit(flashes, pos + Vector3.up, Vector3.zero, 9f * scale, 0.25f, new Color(1f, 0.9f, 0.6f));
        for (int i = 0; i < 18; i++)
            Emit(sparks, pos + Vector3.up, Random.onUnitSphere * Random.Range(10f, 20f) + Vector3.up * 6f, Random.Range(0.4f, 0.7f) * scale, Random.Range(0.4f, 0.8f), new Color(1f, Random.Range(0.4f, 0.8f), 0.2f));
        for (int i = 0; i < 10; i++)
            Emit(puffs, pos + Vector3.up + Random.insideUnitSphere * 1.5f, Random.insideUnitSphere * 3f + Vector3.up * 2f, Random.Range(2.5f, 4f) * scale, Random.Range(0.8f, 1.3f), new Color(0.25f, 0.22f, 0.22f, 0.85f));
    }

    public void BlastRing(Vector3 pos, float radius, Color color)
    {
        Emit(rings, pos + Vector3.up * 0.3f, Vector3.zero, radius * 2.2f, 0.45f, color);
        Emit(flashes, pos + Vector3.up, Vector3.zero, radius * 1.2f, 0.18f, color);
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(sparks, pos + Vector3.up * 0.8f + dir, dir * radius * 3f + Vector3.up * 2f, 0.6f, 0.35f, color);
        }
    }

    public void Dust(Vector3 pos, float scale = 1f)
    {
        for (int i = 0; i < 4; i++)
            Emit(puffs, pos + Random.insideUnitSphere * 0.6f, Random.insideUnitSphere * 2f + Vector3.up, Random.Range(1.2f, 2f) * scale, Random.Range(0.4f, 0.7f), new Color(0.85f, 0.8f, 0.7f, 0.6f));
    }

    /// <summary>A looping smoke column (wrecks).</summary>
    public ParticleSystem CreateSmoke(Transform parent, Vector3 localPos)
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startColor = new Color(0.18f, 0.17f, 0.17f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = ps.emission;
        em.rateOverTime = 8f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.4f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        var colr = ps.colorOverLifetime;
        colr.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colr.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = puffMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    /// <summary>Streaks flying back past the car while Turbo is on.</summary>
    public ParticleSystem CreateSpeedLines(Transform parent)
    {
        var go = new GameObject("SpeedLines");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.2f, 1.5f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startColor = new Color(0.7f, 1f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission;
        em.rateOverTime = 70f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(3.5f, 2f, 1f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-30f, -30f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = glowMaterial;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.12f;
        r.lengthScale = 2f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }
}
