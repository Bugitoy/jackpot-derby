using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>Builds the dice, clover and black cat prefabs, the storm materials, and adds the storm + pickup spawner to the scene.</summary>
public static class PickupPrefabBuilder
{
    const string Folder = "Assets/Prefabs/Pickups";

    [MenuItem("Jackpot Derby/Build Pickups + Storm")]
    public static void BuildAll()
    {
        ModelPrefabUtil.EnsureFolder(Folder);
        var dice = BuildPickup("Dice", "Assets/Models/die.glb", PickupKind.Dice, 1.5f, new Color(1f, 1f, 1f, 0.45f));
        var clover = BuildPickup("Clover", "Assets/Models/clover_pickup.glb", PickupKind.Clover, 1.8f, new Color(0.35f, 1f, 0.4f, 0.5f), hideBase: true);
        var cat = BuildCat();
        AssetDatabase.SaveAssets();
        SetupScene(dice, clover, cat);
    }

    static Pickup BuildPickup(string name, string modelPath, PickupKind kind, float size, Color glow, bool hideBase = false)
    {
        var root = new GameObject(name);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var model = ModelPrefabUtil.PlaceModel(modelPath, visual, size);
        if (model != null && hideBase) HideBaseAndRefit(model, visual, size);
        if (model != null)
        {
            // Spin around the middle of the model, not its base.
            Bounds b = ModelPrefabUtil.LocalBounds(model, root.transform);
            model.transform.localPosition -= new Vector3(0f, b.size.y / 2f, 0f);
            visual.localPosition = new Vector3(0f, b.size.y / 2f, 0f);
        }
        ModelPrefabUtil.NoShadows(root);

        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.7f;
        col.center = new Vector3(0f, 1.2f, 0f);

        var p = root.AddComponent<Pickup>();
        var so = new SerializedObject(p);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("visual").objectReferenceValue = visual;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Pickup>();
    }

    /// <summary>
    /// Hide the flat base the model stands on (e.g. the clover's coin: parts that sit entirely in the
    /// bottom 20% of the model), then rescale so what's left is `size` across and sits on the ground.
    /// </summary>
    static void HideBaseAndRefit(GameObject model, Transform parent, float size)
    {
        Bounds total = ModelPrefabUtil.LocalBounds(model, parent);
        float cut = total.min.y + total.size.y * 0.2f;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            if (r.bounds.max.y < cut) r.gameObject.SetActive(false);

        Bounds rest = ActiveBounds(model, parent);
        float scale = size / Mathf.Max(rest.size.x, rest.size.z, rest.size.y * 0.8f);
        model.transform.localScale *= scale;
        rest = ActiveBounds(model, parent);
        model.transform.localPosition += new Vector3(-rest.center.x, -rest.min.y, -rest.center.z);
    }

    static Bounds ActiveBounds(GameObject model, Transform parent)
    {
        Bounds b = default;
        bool any = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>(false))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        b.center -= parent.position;
        return b;
    }

    static BlackCat BuildCat()
    {
        var root = new GameObject("BlackCat");
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        // The cat model faces -X; turn it 90° so it faces +Z (the way it walks).
        var model = ModelPrefabUtil.PlaceModel("Assets/Models/black_cat.glb", visual, 1.6f, 90f);

        // Unpack so we can add hinges, then hang each leg/tail/head under a pivot at its joint.
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Transform Part(string n)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }
        // Tripo part numbers for this model: 0 head, 7-9 ears, 1 body, 2 tail, 3-4 hind legs, 5-6 front legs.
        var head = Hinge(visual, Part("tripo_part_0"), HingeAt.Bottom);
        foreach (var ear in new[] { "tripo_part_7", "tripo_part_8", "tripo_part_9" })
        {
            var e = Part(ear);
            if (e != null && head != null) e.SetParent(head, true);
        }
        var tail = Hinge(visual, Part("tripo_part_2"), HingeAt.BottomBack);
        var front = new[] { Hinge(visual, Part("tripo_part_5"), HingeAt.Top), Hinge(visual, Part("tripo_part_6"), HingeAt.Top) };
        var back = new[] { Hinge(visual, Part("tripo_part_3"), HingeAt.Top), Hinge(visual, Part("tripo_part_4"), HingeAt.Top) };

        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.2f;
        col.center = new Vector3(0f, 0.8f, 0f);
        root.AddComponent<NavMeshAgent>();
        var cat = root.AddComponent<BlackCat>();
        ModelPrefabUtil.SetField(cat, "visual", visual);
        ModelPrefabUtil.SetField(cat, "head", head);
        ModelPrefabUtil.SetField(cat, "tail", tail);
        ModelPrefabUtil.SetArray(cat, "frontLegs", front);
        ModelPrefabUtil.SetArray(cat, "backLegs", back);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/BlackCat.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<BlackCat>();
    }

    enum HingeAt { Top, Bottom, BottomBack }

    /// <summary>Put `part` under a new pivot at its joint, aligned with the cat (local X = cat's right).</summary>
    static Transform Hinge(Transform visual, Transform part, HingeAt where)
    {
        if (part == null) return null;
        var r = part.GetComponent<Renderer>();
        Bounds b = r != null ? r.bounds : new Bounds(part.position, Vector3.zero);
        Vector3 joint;
        switch (where)
        {
            case HingeAt.Top: joint = new Vector3(b.center.x, b.max.y, b.center.z); break;
            case HingeAt.Bottom: joint = new Vector3(b.center.x, b.min.y, b.center.z); break;
            default: joint = new Vector3(b.center.x, b.min.y + b.size.y * 0.15f, b.max.z); break; // tail root, toward the body
        }
        var pivot = new GameObject(part.name + "_Hinge").transform;
        pivot.SetParent(part.parent, false);
        pivot.position = joint;
        pivot.rotation = visual.rotation;
        part.SetParent(pivot, true);
        return pivot;
    }

    static Material AlphaMaterial(string name, Color c)
    {
        var m = ModelPrefabUtil.GlowMaterial(name, c);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        EditorUtility.SetDirty(m);
        return m;
    }

    // The Bad Luck Storm is a dark, dusty-yellow sandstorm.
    static Material StormWallMaterial() => MistMaterial("StormMistWall", false, new Color(0.55f, 0.44f, 0.22f), new Color(0.24f, 0.17f, 0.07f), 0.8f);
    static Material StormGroundMaterial() => MistMaterial("StormMistGround", true, new Color(0.26f, 0.19f, 0.08f), new Color(0.12f, 0.08f, 0.03f), 0.55f);

    /// <summary>Re-colours the existing storm materials and in-storm grade without rebuilding the pickups.</summary>
    [MenuItem("Jackpot Derby/Apply Storm Look")]
    public static void ApplyStormLook()
    {
        StormWallMaterial();
        StormGroundMaterial();
        LookSetup.BuildStormVolume();
        AssetDatabase.SaveAssets();
        Debug.Log("[Storm] Sandstorm look applied.");
    }

    /// <summary>Material on the storm mist shader (wall curtain or ground darkening).</summary>
    static Material MistMaterial(string name, bool ground, Color light, Color deep, float maxAlpha)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/FX");
        string path = $"Assets/Materials/FX/{name}.mat";
        var shader = Shader.Find("JackpotDerby/StormMist");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_Color", light);
        m.SetColor("_DeepColor", deep);
        m.SetFloat("_MaxAlpha", maxAlpha);
        m.SetFloat("_GroundMode", ground ? 1f : 0f);
        m.SetFloat("_NoiseScale", ground ? 0.05f : 0.07f);
        m.SetFloat("_ScrollSpeed", ground ? 0.4f : 0.6f);
        m.SetFloat("_Height", 14f);
        m.SetFloat("_EdgeSoftness", 10f);
        m.SetFloat("_SoftDepth", 2f);
        m.renderQueue = ground ? 2990 : 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetupScene(Pickup dice, Pickup clover, BlackCat cat)
    {
        var sys = GameObject.Find("GameSystems");
        if (sys == null) sys = new GameObject("GameSystems");
        var spawner = sys.GetComponent<PickupSpawner>();
        if (spawner == null) spawner = sys.AddComponent<PickupSpawner>();
        ModelPrefabUtil.SetField(spawner, "dicePrefab", dice);
        ModelPrefabUtil.SetField(spawner, "cloverPrefab", clover);
        ModelPrefabUtil.SetField(spawner, "catPrefab", cat);
        ModelPrefabUtil.SetField(spawner, "grid", Object.FindAnyObjectByType<ArenaGrid>());

        var stormGo = GameObject.Find("BadLuckStorm");
        if (stormGo == null) stormGo = new GameObject("BadLuckStorm");
        var storm = stormGo.GetComponent<StormController>();
        if (storm == null) storm = stormGo.AddComponent<StormController>();
        ModelPrefabUtil.SetField(storm, "grid", Object.FindAnyObjectByType<ArenaGrid>());
        ModelPrefabUtil.SetField(storm, "wallMaterial", StormWallMaterial());
        ModelPrefabUtil.SetField(storm, "groundMaterial", StormGroundMaterial());
        var vg = GameObject.Find("StormVolume");
        if (vg != null) ModelPrefabUtil.SetField(storm, "stormVolume", vg.GetComponent<UnityEngine.Rendering.Volume>());
        ModelPrefabUtil.SetField(storm, "cloudMaterial", CombatAssetBuilder.ParticleMaterial("FxPuff", false));

        EditorSceneManager.MarkSceneDirty(sys.scene);
    }
}
