using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One click: build the arena, the car prefab, and wire up camera, light and the player.</summary>
public static class SceneSetup
{
    [MenuItem("Jackpot Derby/Rebuild Whole Scene")]
    public static void RebuildAll()
    {
        ArenaBuilder.BuildArena();
        CombatAssetBuilder.BuildAll();
        CarPrefabBuilder.BuildAll();
        ChestPrefabBuilder.BuildAll();
        SetupPlayerCameraLight();
        SetupGameSystems();
        AISetup.BakeNavMesh();
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Jackpot Derby/Rebuild Prefabs + Systems (keep arena)")]
    public static void RebuildPrefabsAndSystems()
    {
        CombatAssetBuilder.BuildAll();
        CarPrefabBuilder.BuildAll();
        ChestPrefabBuilder.BuildAll();
        SetupPlayerCameraLight();
        SetupGameSystems();
        AISetup.BakeNavMesh();
        EditorSceneManager.SaveOpenScenes();
    }

    /// <summary>
    /// Chunky HUD text: thick dark outline + drop shadow. Saved as an asset so the WebGL build keeps
    /// the outline/shadow shader variant.
    /// </summary>
    static Material OutlinedFontMaterial()
    {
        const string path = "Assets/Materials/UI/HudText_Outlined.mat";
        ModelPrefabUtil.EnsureFolder("Assets/Materials/UI");
        var font = TMPro.TMP_Settings.defaultFontAsset;
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(font.material);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = font.material.shader;
        m.SetTexture(TMPro.ShaderUtilities.ID_MainTex, font.material.GetTexture(TMPro.ShaderUtilities.ID_MainTex));
        m.EnableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
        m.SetFloat(TMPro.ShaderUtilities.ID_FaceDilate, 0.15f);
        m.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, 0.22f);
        m.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, new Color(0.08f, 0.08f, 0.1f, 1f));
        m.EnableKeyword(TMPro.ShaderUtilities.Keyword_Underlay);
        m.SetColor(TMPro.ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
        m.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
        m.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
        m.SetFloat(TMPro.ShaderUtilities.ID_UnderlaySoftness, 0.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>The "GameSystems" object: chest spawner, HUD, debug cheats.</summary>
    public static void SetupGameSystems()
    {
        var sys = GameObject.Find("GameSystems");
        if (sys == null) sys = new GameObject("GameSystems");

        var spawner = sys.GetComponent<ChestSpawner>();
        if (spawner == null) spawner = sys.AddComponent<ChestSpawner>();
        ModelPrefabUtil.SetField(spawner, "chestPrefab", AssetDatabase.LoadAssetAtPath<Chest>(ChestPrefabBuilder.ChestPath));
        ModelPrefabUtil.SetField(spawner, "jackpotChestPrefab", AssetDatabase.LoadAssetAtPath<Chest>(ChestPrefabBuilder.JackpotPath));
        ModelPrefabUtil.SetField(spawner, "grid", Object.FindAnyObjectByType<ArenaGrid>());

        var hud = sys.GetComponent<Hud>();
        if (hud == null) hud = sys.AddComponent<Hud>();
        ModelPrefabUtil.SetField(hud, "outlinedText", OutlinedFontMaterial());
        if (sys.GetComponent<DebugCheats>() == null) sys.AddComponent<DebugCheats>();
        if (sys.GetComponent<MatchManager>() == null) sys.AddComponent<MatchManager>();

        var fx = sys.GetComponent<FxLibrary>();
        if (fx == null) fx = sys.AddComponent<FxLibrary>();
        ModelPrefabUtil.SetField(fx, "glowMaterial", CombatAssetBuilder.ParticleMaterial("FxGlow", true));
        ModelPrefabUtil.SetField(fx, "puffMaterial", CombatAssetBuilder.ParticleMaterial("FxPuff", false));
        ModelPrefabUtil.SetField(fx, "coinMaterial", CombatAssetBuilder.CoinMaterial());
        ModelPrefabUtil.SetField(fx, "coinMesh", CombatAssetBuilder.CoinMesh());
        ModelPrefabUtil.SetField(fx, "ringMaterial", CombatAssetBuilder.RingParticleMaterial());
        EditorSceneManager.MarkSceneDirty(sys.scene);
    }

    public static void SetupPlayerCameraLight()
    {
        // Player car at the Lucky 7 garage.
        var spawn = GameObject.Find("Spawn_Lucky7");
        var old = GameObject.Find("PlayerCar");
        if (old != null) Object.DestroyImmediate(old);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabBuilder.PlayerPrefabPath);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.name = "PlayerCar";
        if (spawn != null) player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);

        // Enemy cars in their garages (they just sit there until the AI arrives in Milestone 5).
        foreach (var e in CarPrefabBuilder.Enemies)
        {
            var existing = GameObject.Find(e.prefab);
            if (existing != null) Object.DestroyImmediate(existing);
            var ePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/{e.prefab}.prefab");
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(ePrefab);
            enemy.name = e.prefab;
            var eSpawn = GameObject.Find(e.spawn);
            if (eSpawn != null) enemy.transform.SetPositionAndRotation(eSpawn.transform.position, eSpawn.transform.rotation);
        }

        // Camera.
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        var chase = cam.GetComponent<ChaseCamera>();
        if (chase == null) chase = cam.gameObject.AddComponent<ChaseCamera>();
        chase.target = player.transform;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.70f, 0.80f);
        if (cam.GetComponent<FpsCounter>() == null) cam.gameObject.AddComponent<FpsCounter>();
        if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();

        // Sun.
        var light = Object.FindAnyObjectByType<Light>();
        if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        light.intensity = 1.3f;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.80f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.25f);

        EditorSceneManager.MarkSceneDirty(player.scene);
    }
}
