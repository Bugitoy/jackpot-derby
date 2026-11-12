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
        CarPrefabBuilder.BuildAll();
        SetupPlayerCameraLight();
        EditorSceneManager.SaveOpenScenes();
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
