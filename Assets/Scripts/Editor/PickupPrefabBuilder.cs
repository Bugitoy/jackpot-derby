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

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "GroundGlow";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        disc.transform.localScale = new Vector3(size * 1.6f, 0.01f, size * 1.6f);
        disc.GetComponent<Renderer>().sharedMaterial = ModelPrefabUtil.GlowMaterial(name + "Glow", glow);
        ModelPrefabUtil.NoShadows(disc);

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
        ModelPrefabUtil.PlaceModel("Assets/Models/black_cat.glb", visual, 1.4f);

        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.2f;
        col.center = new Vector3(0f, 0.8f, 0f);
        root.AddComponent<NavMeshAgent>();
        var cat = root.AddComponent<BlackCat>();
        ModelPrefabUtil.SetField(cat, "visual", visual);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/BlackCat.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<BlackCat>();
    }

    static Material AlphaMaterial(string name, Color c)
    {
        var m = ModelPrefabUtil.GlowMaterial(name, c);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
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
        ModelPrefabUtil.SetField(storm, "wallMaterial", AlphaMaterial("StormWall", new Color(0.45f, 0.18f, 0.7f, 0.32f)));
        ModelPrefabUtil.SetField(storm, "groundMaterial", AlphaMaterial("StormGround", new Color(0.12f, 0.04f, 0.2f, 0.5f)));
        ModelPrefabUtil.SetField(storm, "cloudMaterial", CombatAssetBuilder.ParticleMaterial("FxPuff", false));

        EditorSceneManager.MarkSceneDirty(sys.scene);
    }
}
