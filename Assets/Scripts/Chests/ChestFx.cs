using TMPro;
using UnityEngine;

/// <summary>
/// The look of a chest, after Lefika's chest concept (Docs/Menu_Assets/chest_concept.jpg):
///  * a soft pillar of light with streaks rising up it (LightPillar shader),
///  * glowing rings on the ground with swirling arcs and outward ripples (ChestRings shader),
///  * a glossy floating token above the chest, "?" (gold) or "$" (Jackpot, red), bobbing and spinning,
///    with little gears (or, for the Jackpot, trailing sparks) orbiting it,
///  * embers rising off the ground, light streaks shooting up the pillar, dust kicked up at the base.
/// The chest's spawn grows the pillar in; PlayOpen() flares everything out when the chest is cracked.
/// Everything is built in code at Awake from the materials the prefab carries.
/// </summary>
public class ChestFx : MonoBehaviour
{
    [SerializeField] bool jackpot;
    [Header("Materials (made by ChestPrefabBuilder)")]
    [SerializeField] Material pillarMaterial;
    [SerializeField] Material ringsMaterial;
    [SerializeField] Material glowParticleMaterial;
    [SerializeField] Material puffParticleMaterial;
    [SerializeField] Material gearParticleMaterial;
    [SerializeField] Material tokenMaterial;
    [SerializeField] Material tokenRimMaterial;
    [SerializeField] TMP_FontAsset symbolFont;

    [Header("Shape")]
    [SerializeField] float pillarHeight = 42f;
    [SerializeField] float pillarRadius = 1.5f;
    [SerializeField] float ringSize = 7.5f;
    [SerializeField] float tokenHeight = 4.4f;
    [SerializeField] float tokenSize = 1.5f;

    public Color MainColor => jackpot ? new Color(1f, 0.3f, 0.22f) : new Color(1f, 0.8f, 0.25f);

    Material pillarMat, ringsMat, glowMat;
    Transform pillar, tokenAnchor, token, glowQuad;
    Light pillarLight;
    ParticleSystem embers, streaks, dust, orbit;
    float age, burst = 1f, openAge = -1f;
    static Texture2D gearTex;
    static Mesh openCylinder, flatQuad;

    void Awake()
    {
        Color c = MainColor;

        // Pillar of light.
        pillar = MakeMesh("Pillar", OpenCylinder(), pillarMaterial, out pillarMat);
        pillarMat.SetColor("_Color", new Color(c.r, c.g, c.b, jackpot ? 0.85f : 0.75f));
        pillarMat.SetColor("_StreakColor", jackpot ? new Color(1f, 0.75f, 0.55f) : new Color(1f, 0.97f, 0.7f));
        pillar.localScale = new Vector3(pillarRadius, 0.01f, pillarRadius);

        // Rings on the ground.
        var rings = MakeMesh("GroundRings", FlatQuad(), ringsMaterial, out ringsMat);
        rings.localPosition = new Vector3(0f, 0.06f, 0f);
        rings.localScale = Vector3.one * ringSize;
        ringsMat.SetColor("_Color", c);

        // Floating token.
        tokenAnchor = new GameObject("TokenAnchor").transform;
        tokenAnchor.SetParent(transform, false);
        tokenAnchor.localPosition = new Vector3(0f, tokenHeight, 0f);
        token = BuildToken(c);
        glowQuad = MakeMesh("TokenGlow", FlatQuad(), glowParticleMaterial, out glowMat);
        glowQuad.SetParent(tokenAnchor, false);
        glowQuad.localScale = Vector3.one * tokenSize * 3.4f;
        glowMat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.8f));

        // Coloured light inside the pillar, so the chest and the ground pick up its glow.
        var lightGo = new GameObject("PillarLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        pillarLight = lightGo.AddComponent<Light>();
        pillarLight.type = LightType.Point;
        pillarLight.color = c;
        pillarLight.range = ringSize * 0.9f;
        pillarLight.intensity = 0f;
        pillarLight.shadows = LightShadows.None;

        BuildParticles(c);
    }

    // ---------------- building ----------------

    Transform MakeMesh(string name, Mesh mesh, Material source, out Material instance)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        instance = source != null ? new Material(source) : null;
        r.sharedMaterial = instance;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    Transform BuildToken(Color c)
    {
        var t = new GameObject("Token").transform;
        t.SetParent(tokenAnchor, false);
        float thick = tokenSize * 0.12f;

        // Coin body (faces along Z), a darker rim behind it, and the symbol on both faces.
        var body = Disc("Body", t, tokenSize, thick, tokenMaterial, c);
        var rim = Disc("Rim", t, tokenSize * 1.1f, thick * 0.7f, tokenRimMaterial, Color.Lerp(c, Color.black, 0.45f));
        string symbol = jackpot ? "$" : "?";
        Color ink = jackpot ? new Color(0.3f, 0.04f, 0.03f) : new Color(0.28f, 0.15f, 0.02f);
        for (int side = 0; side < 2; side++)
        {
            var go = new GameObject(side == 0 ? "SymbolFront" : "SymbolBack");
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0f, 0f, (side == 0 ? -1f : 1f) * (thick * 0.5f + 0.01f));
            go.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            if (symbolFont != null) tmp.font = symbolFont;
            tmp.text = symbol;
            tmp.fontSize = tokenSize * 9f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = ink;
            tmp.rectTransform.sizeDelta = new Vector2(tokenSize, tokenSize);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return t;
    }

    Transform Disc(string name, Transform parent, float diameter, float thickness, Material source, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);
        var r = go.GetComponent<MeshRenderer>();
        if (source != null)
        {
            var m = new Material(source);
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 0.9f);
            r.sharedMaterial = m;
        }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    void BuildParticles(Color c)
    {
        Color bright = Color.Lerp(c, Color.white, 0.45f);

        // Embers drifting up off the glowing ground.
        embers = NewSystem("Embers", glowParticleMaterial, transform, 90);
        var m = embers.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        m.startColor = new ParticleSystem.MinMaxGradient(c, bright);
        m.gravityModifier = -0.12f;
        var sh = embers.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = ringSize * 0.32f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        sh.randomDirectionAmount = 0.25f;
        var em = embers.emission; em.rateOverTime = jackpot ? 28f : 20f;
        var noise = embers.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.6f;
        FadeOut(embers);

        // Light streaks shooting up inside the pillar.
        streaks = NewSystem("Streaks", glowParticleMaterial, transform, 40);
        m = streaks.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(9f, 15f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(bright.r, bright.g, bright.b, 0.7f));
        sh = streaks.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = pillarRadius * 0.85f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        sh.randomDirectionAmount = 0f;
        em = streaks.emission; em.rateOverTime = 9f;
        var sr = streaks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.22f;
        sr.lengthScale = 2f;
        FadeOut(streaks);

        // Dust and grit kicked up at the base (the Jackpot chest throws hot sparks instead).
        dust = NewSystem("Dust", jackpot ? glowParticleMaterial : puffParticleMaterial, transform, 60);
        m = dust.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        m.startSize = jackpot ? new ParticleSystem.MinMaxCurve(0.05f, 0.11f) : new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        m.startColor = jackpot ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.3f), new Color(1f, 0.4f, 0.2f))
                               : new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.3f, 0.18f), new Color(0.6f, 0.45f, 0.28f));
        m.gravityModifier = 1.4f;
        sh = dust.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 50f;
        sh.radius = 1.4f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        em = dust.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0.2f, 6, 10, 0, 0.9f) });
        FadeOut(dust);

        // Orbiting the token: little gears (?) or trailing sparks ($).
        orbit = NewSystem("Orbit", jackpot ? glowParticleMaterial : GearMaterial(), tokenAnchor, 30);
        m = orbit.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
        m.startSpeed = 0f;
        m.startSize = jackpot ? new ParticleSystem.MinMaxCurve(0.1f, 0.16f) : new ParticleSystem.MinMaxCurve(0.32f, 0.5f);
        m.startColor = jackpot ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.25f))
                               : new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.62f, 0.2f), new Color(0.72f, 0.42f, 0.14f));
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        sh = orbit.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = tokenSize * 0.95f;
        sh.radiusThickness = 0f;
        sh.rotation = new Vector3(-90f, 0f, 0f);        // circle lies flat, round the token
        em = orbit.emission; em.rateOverTime = jackpot ? 7f : 3.5f;
        var vel = orbit.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(jackpot ? 3.2f : 1.6f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        // Unity wants x/y/z (and the three orbital axes) in the same curve mode: all "random between two".
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        if (!jackpot)
        {
            var rot = orbit.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        }
        else
        {
            var trails = orbit.trails;
            trails.enabled = true;
            trails.lifetime = 0.35f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            trails.inheritParticleColor = true;
            orbit.GetComponent<ParticleSystemRenderer>().trailMaterial = glowParticleMaterial;
        }
        FadeOut(orbit);
    }

    ParticleSystem NewSystem(string name, Material mat, Transform parent, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.maxParticles = max;
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.playOnAwake = true;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    Material GearMaterial()
    {
        if (gearParticleMaterial == null) return glowParticleMaterial;
        var m = new Material(gearParticleMaterial);
        m.SetTexture("_BaseMap", GearTexture());
        return m;
    }

    /// <summary>A little cog (white, soft-edged) drawn once in code, tinted per particle.</summary>
    static Texture2D GearTexture()
    {
        if (gearTex != null) return gearTex;
        const int size = 64;
        gearTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
            float teeth = Mathf.Clamp01(Mathf.Cos(a * 8f) * 3f);           // 8 square-ish teeth
            float outer = Mathf.Lerp(0.72f, 0.92f, teeth);
            float body = Mathf.Clamp01((outer - r) * size * 0.5f) * Mathf.Clamp01((r - 0.26f) * size * 0.5f);
            float shade = 0.75f + 0.25f * Mathf.Clamp01(1f - r);              // a touch lighter toward the hub
            px[y * size + x] = new Color(shade, shade, shade, body);
        }
        gearTex.SetPixels32(px);
        gearTex.Apply();
        return gearTex;
    }

    static Mesh OpenCylinder()
    {
        if (openCylinder != null) return openCylinder;
        const int seg = 32;
        var v = new Vector3[(seg + 1) * 2];
        var n = new Vector3[v.Length];
        var uv = new Vector2[v.Length];
        var tri = new int[seg * 6];
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v[i * 2] = d; v[i * 2 + 1] = d + Vector3.up;
            n[i * 2] = n[i * 2 + 1] = d;
            uv[i * 2] = new Vector2(i / (float)seg, 0f); uv[i * 2 + 1] = new Vector2(i / (float)seg, 1f);
            if (i == seg) break;
            int k = i * 6, b = i * 2;
            tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
            tri[k + 3] = b + 2; tri[k + 4] = b + 1; tri[k + 5] = b + 3;
        }
        openCylinder = new Mesh { name = "OpenCylinder", vertices = v, normals = n, uv = uv, triangles = tri };
        openCylinder.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 2f));
        return openCylinder;
    }

    static Mesh FlatQuad()
    {
        if (flatQuad != null) return flatQuad;
        flatQuad = new Mesh
        {
            name = "FlatQuad",
            vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
            uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) },
            normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
        };
        return flatQuad;
    }

    // ---------------- animation ----------------

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        burst = Mathf.Max(0f, burst - dt * 1.4f);

        if (openAge >= 0f) { UpdateOpen(dt); return; }

        // Spawn: the pillar shoots up out of the ground, the token pops in.
        float grow = Mathf.Clamp01(age / 0.55f);
        float growE = 1f - Mathf.Pow(1f - grow, 3f);
        pillar.localScale = new Vector3(pillarRadius * (1f + 0.6f * (1f - growE)), pillarHeight * Mathf.Max(0.01f, growE), pillarRadius * (1f + 0.6f * (1f - growE)));
        pillarMat.SetFloat("_Intensity", 0.85f + 0.15f * Mathf.Sin(age * 2.6f) + burst * 1.2f);
        ringsMat.SetFloat("_Burst", burst);

        float tokenIn = Mathf.Clamp01((age - 0.3f) / 0.4f);
        float pop = tokenIn < 1f ? Mathf.Sin(tokenIn * Mathf.PI * 0.5f) * (1f + 0.3f * Mathf.Sin(tokenIn * Mathf.PI)) : 1f;
        token.localScale = Vector3.one * Mathf.Max(0.001f, pop);
        token.localPosition = new Vector3(0f, Mathf.Sin(age * 2f) * 0.25f, 0f);
        token.localRotation = Quaternion.Euler(0f, age * 110f, Mathf.Sin(age * 1.3f) * 6f);
        glowMat.SetColor("_BaseColor", WithAlpha(MainColor, 0.7f + 0.15f * Mathf.Sin(age * 3f)));
        pillarLight.intensity = growE * (jackpot ? 4.5f : 3.5f) * (0.9f + 0.1f * Mathf.Sin(age * 2.6f)) + burst * 3f;
    }

    void LateUpdate()
    {
        // The glow behind the token always faces the camera.
        // (The quad's face is its local +Y, so point +Y at the camera.)
        var cam = Camera.main;
        if (cam != null && glowQuad != null)
            glowQuad.rotation = Quaternion.LookRotation(cam.transform.up, -cam.transform.forward);
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    /// <summary>Chest cracked open: the pillar flares and collapses, rings blast out, the token shoots up.</summary>
    public void PlayOpen()
    {
        if (openAge >= 0f) return;
        openAge = 0f;
        burst = 1f;
        embers.Emit(45);
        streaks.Emit(18);
        dust.Emit(16);
        var em = orbit.emission; em.enabled = false;
        em = embers.emission; em.enabled = false;
        em = streaks.emission; em.enabled = false;
        em = dust.emission; em.enabled = false;
    }

    void UpdateOpen(float dt)
    {
        openAge += dt;
        float t = Mathf.Clamp01(openAge / 0.8f);
        // Pillar: a bright flash and a widening collapse.
        pillarMat.SetFloat("_Intensity", Mathf.Lerp(2.4f, 0f, t));
        float w = pillarRadius * (1f + t * 1.6f);
        pillar.localScale = new Vector3(w, pillarHeight * (1f + t * 0.3f), w);
        ringsMat.SetFloat("_Burst", 1f - t * 0.6f);
        ringsMat.SetFloat("_Intensity", 1f - t);
        // Token: shoots up spinning fast, then pops away.
        token.localPosition = new Vector3(0f, 0.25f + t * 6f, 0f);
        token.localRotation = Quaternion.Euler(0f, age * 110f + openAge * 900f, 0f);
        token.localScale = Vector3.one * Mathf.Max(0.001f, t < 0.7f ? 1f + t * 0.5f : Mathf.Lerp(1.35f, 0f, (t - 0.7f) / 0.3f));
        glowMat.SetColor("_BaseColor", WithAlpha(MainColor, Mathf.Lerp(0.9f, 0f, t)));
        pillarLight.intensity = Mathf.Lerp(9f, 0f, t);
    }
}
