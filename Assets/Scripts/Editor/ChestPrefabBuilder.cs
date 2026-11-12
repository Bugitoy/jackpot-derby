using UnityEditor;
using UnityEngine;

/// <summary>Builds Chest and ChestJackpot prefabs: model, box collider, Chest script, glowing light beam.</summary>
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

        // Light beam: a tall see-through cylinder fading upward, so everyone sees where the chest is.
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "LightBeam";
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.transform.SetParent(root.transform, false);
        float height = jackpot ? 60f : 45f;
        beam.transform.localPosition = new Vector3(0f, height / 2f, 0f);
        beam.transform.localScale = new Vector3(size * 0.8f, height / 2f, size * 0.8f);
        var beamMat = ModelPrefabUtil.GlowMaterial(jackpot ? "ChestBeamJackpot" : "ChestBeam", beamColor, ModelPrefabUtil.BeamGradient());
        var beamRenderer = beam.GetComponent<Renderer>();
        beamRenderer.sharedMaterial = beamMat;
        ModelPrefabUtil.NoShadows(beam);

        // Glow disc on the ground.
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "GroundGlow";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        disc.transform.localScale = new Vector3(size * 2f, 0.01f, size * 2f);
        var c = beamColor; c.a *= 0.5f;
        disc.GetComponent<Renderer>().sharedMaterial = ModelPrefabUtil.GlowMaterial(jackpot ? "ChestGlowJackpot" : "ChestGlow", c);
        ModelPrefabUtil.NoShadows(disc);

        var chest = root.AddComponent<Chest>();
        ModelPrefabUtil.SetBool(chest, "isJackpot", jackpot);
        ModelPrefabUtil.SetField(chest, "visual", visual);
        ModelPrefabUtil.SetField(chest, "beam", beamRenderer);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }
}
