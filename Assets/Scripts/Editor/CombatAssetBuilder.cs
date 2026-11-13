using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds combat assets: the two blaster bolt prefabs, particle materials, the ground range-ring
/// material, and generated textures (soft dot, ring).
/// </summary>
public static class CombatAssetBuilder
{
    public const string SmallBoltPath = "Assets/Prefabs/Combat/BoltSmall.prefab";
    public const string JackpotBoltPath = "Assets/Prefabs/Combat/BoltJackpot.prefab";

    [MenuItem("Jackpot Derby/Build Combat Assets")]
    public static void BuildAll()
    {
        ModelPrefabUtil.EnsureFolder("Assets/Prefabs/Combat");
        // Small blaster: fat teal-green "lucky bolt".
        BuildBolt(SmallBoltPath, "BoltSmall", new Color(0.35f, 1f, 0.75f), new Color(0.6f, 1f, 0.9f), 0.75f, 34f, 1.3f, 1f);
        // Jackpot cannon: bigger gold-and-red burst.
        BuildBolt(JackpotBoltPath, "BoltJackpot", new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.3f), 0.95f, 38f, 1.0f, 1.25f);
        AssetDatabase.SaveAssets();
    }

    static void BuildBolt(string path, string name, Color core, Color glow, float size, float speed, float arc, float hitScale)
    {
        var root = new GameObject(name);

        var coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreGo.name = "Core";
        Object.DestroyImmediate(coreGo.GetComponent<Collider>());
        coreGo.transform.SetParent(root.transform, false);
        coreGo.transform.localScale = new Vector3(size * 0.75f, size * 0.75f, size * 1.25f);
        coreGo.GetComponent<Renderer>().sharedMaterial = UnlitColor(name + "_Core", Color.Lerp(core, Color.white, 0.55f));
        ModelPrefabUtil.NoShadows(coreGo);

        var glowGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glowGo.name = "Glow";
        Object.DestroyImmediate(glowGo.GetComponent<Collider>());
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localScale = new Vector3(size * 1.6f, size * 1.6f, size * 2.2f);
        var g = core; g.a = 0.55f;
        glowGo.GetComponent<Renderer>().sharedMaterial = ModelPrefabUtil.GlowMaterial(name + "_Glow", g);
        ModelPrefabUtil.NoShadows(glowGo);

        var trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.14f;
        trail.minVertexDistance = 0.2f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, size * 1.1f), new Keyframe(1f, 0f));
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(glow, 0f), new GradientColorKey(core, 1f) },
                     new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
        trail.sharedMaterial = ParticleMaterial("FxGlow", true);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;

        var p = root.AddComponent<Projectile>();
        var so = new SerializedObject(p);
        so.FindProperty("speed").floatValue = speed;
        so.FindProperty("arcHeight").floatValue = arc;
        so.FindProperty("hitColor").colorValue = core;
        so.FindProperty("hitScale").floatValue = hitScale;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static Material UnlitColor(string name, Color c)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/FX");
        string path = $"Assets/Materials/FX/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>URP particle material. additive = glows; otherwise soft alpha-blended puffs.</summary>
    public static Material ParticleMaterial(string name, bool additive)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/FX");
        string path = $"Assets/Materials/FX/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", SoftDot());
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material RingParticleMaterial()
    {
        var m = ParticleMaterial("FxRing", true);
        m.SetTexture("_BaseMap", RingTexture());
        return m;
    }

    public static Material CoinMaterial()
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/FX");
        const string path = "Assets/Materials/FX/FxCoin.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(1f, 0.8f, 0.25f));
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Smoothness", 0.7f);
        m.SetColor("_EmissionColor", new Color(0.35f, 0.25f, 0.05f));
        m.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Soft ring on the ground showing a car's attack range.</summary>
    public static Material RangeRingMaterial()
    {
        var m = ModelPrefabUtil.GlowMaterial("RangeRing", new Color(0.4f, 1f, 0.9f, 0.35f), RingTexture());
        // Regular alpha blending reads better than additive on bright ground.
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A flat disc (squashed built-in cylinder) for coin particles, saved as an asset.</summary>
    public static Mesh CoinMesh()
    {
        const string path = "Assets/Materials/FX/CoinDisc.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var src = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        var mesh = Object.Instantiate(src);
        var v = mesh.vertices;
        for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x, v[i].y * 0.12f, v[i].z);
        mesh.vertices = v;
        mesh.RecalculateBounds();
        mesh.name = "CoinDisc";
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Texture2D SoftDot() => GeneratedTexture("soft_dot", 64, (u, v) =>
    {
        float d = Mathf.Sqrt(u * u + v * v);
        return Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
    });

    static Texture2D RingTexture() => GeneratedTexture("ring_soft", 256, (u, v) =>
    {
        float d = Mathf.Sqrt(u * u + v * v);
        float edge = Mathf.Exp(-Mathf.Pow((d - 0.93f) / 0.045f, 2f));      // bright rim
        float fill = d < 0.95f ? Mathf.Lerp(0.18f, 0.32f, d) : 0f;          // faint fill
        return Mathf.Clamp01(edge + fill);
    });

    /// <summary>Make (once) a white texture whose alpha comes from f(u, v), u/v in -1..1.</summary>
    static Texture2D GeneratedTexture(string name, int size, System.Func<float, float, float> alpha)
    {
        string path = $"Assets/Textures/Generated/{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        ModelPrefabUtil.EnsureFolder("Assets/Textures/Generated");
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(u, v)));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
