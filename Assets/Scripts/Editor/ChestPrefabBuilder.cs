using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Chest and ChestJackpot prefabs: model, box collider, Chest script, and ChestFx (pillar of light,
/// ground rings, floating ?/$ token, particles - after Docs/Menu_Assets/chest_concept.jpg).
/// </summary>
public static class ChestPrefabBuilder
{
    public const string ChestPath = "Assets/Prefabs/Pickups/Chest.prefab";
    public const string JackpotPath = "Assets/Prefabs/Pickups/ChestJackpot.prefab";

    [MenuItem("Jackpot Derby/Build Chest Prefabs")]
    public static void BuildAll()
    {
        ModelPrefabUtil.EnsureFolder("Assets/Prefabs/Pickups");
        Build("Chest", "Assets/Models/chest.glb", ChestPath, false, 2.8f, new Color(1f, 0.85f, 0.35f, 0.55f));
        Build("ChestJackpot", "Assets/Models/chest_jackpot.glb", JackpotPath, true, 3.3f, new Color(1f, 0.35f, 0.25f, 0.6f));
        AssetDatabase.SaveAssets();
    }

    static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material ShaderMaterial(string name, string shader)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/FX");
        string path = $"Assets/Materials/FX/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find(shader);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Glossy, softly glowing coin material for the floating token (colour set per chest).</summary>
    static Material TokenMaterial(string name, float smoothness)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/FX");
        string path = $"Assets/Materials/FX/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_EmissionColor", new Color(0.6f, 0.45f, 0.1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Build(string name, string modelPath, string prefabPath, bool jackpot, float size, Color beamColor)
    {
        var root = new GameObject(name);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var model = ModelPrefabUtil.PlaceModel(modelPath, visual, size);
        Bounds b = ModelPrefabUtil.LocalBounds(model, root.transform);

        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(Mathf.Max(b.size.x, b.size.z) * 0.9f, b.size.y + 0.4f, Mathf.Max(b.size.x, b.size.z) * 0.9f);
        box.center = new Vector3(0f, box.size.y / 2f, 0f);

        // Light, rings, token and particles are built at runtime by ChestFx from these materials.
        var fx = root.AddComponent<ChestFx>();
        ModelPrefabUtil.SetBool(fx, "jackpot", jackpot);
        ModelPrefabUtil.SetField(fx, "pillarMaterial", ShaderMaterial("ChestPillar", "JackpotDerby/LightPillar"));
        ModelPrefabUtil.SetField(fx, "ringsMaterial", ShaderMaterial("ChestRings", "JackpotDerby/ChestRings"));
        ModelPrefabUtil.SetField(fx, "glowParticleMaterial", CombatAssetBuilder.ParticleMaterial("FxGlow", true));
        ModelPrefabUtil.SetField(fx, "puffParticleMaterial", CombatAssetBuilder.ParticleMaterial("FxPuff", false));
        ModelPrefabUtil.SetField(fx, "gearParticleMaterial", CombatAssetBuilder.ParticleMaterial("FxGear", false));
        ModelPrefabUtil.SetField(fx, "tokenMaterial", TokenMaterial("ChestToken", 0.88f));
        ModelPrefabUtil.SetField(fx, "tokenRimMaterial", TokenMaterial("ChestTokenRim", 0.6f));
        ModelPrefabUtil.SetField(fx, "symbolFont", AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Resources/UIFonts/Poppins-ExtraBold SDF.asset"));
        SetFloat(fx, "pillarHeight", jackpot ? 52f : 42f);
        SetFloat(fx, "pillarRadius", size * 0.55f);
        SetFloat(fx, "ringSize", size * 2.7f);
        SetFloat(fx, "tokenHeight", b.size.y + 2.6f);

        var chest = root.AddComponent<Chest>();
        ModelPrefabUtil.SetBool(chest, "isJackpot", jackpot);
        ModelPrefabUtil.SetField(chest, "visual", visual);
        ModelPrefabUtil.SetField(chest, "fx", fx);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }
}
