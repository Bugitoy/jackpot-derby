using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lighting and post-processing for the cartoon look: warm sun with soft shadows, bright trilight
/// ambient, and a global volume (neutral tonemap, punchier colour, light bloom, soft vignette).
/// Menu: Jackpot Derby > Apply Lighting + Post.
/// </summary>
public static class LookSetup
{
    const string ProfilePath = "Assets/Settings/ArenaVolumeProfile.asset";

    [MenuItem("Jackpot Derby/Apply Lighting + Post")]
    public static void Apply()
    {
        var light = Object.FindAnyObjectByType<Light>();
        if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(SunElevation, SunAzimuth, 0f);
        light.intensity = 1.55f;
        light.color = new Color(1f, 0.93f, 0.80f);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.84f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.70f, 0.68f, 0.62f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.38f, 0.32f);

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var tone = Get<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.Neutral);
        var color = Get<ColorAdjustments>(profile);
        color.postExposure.Override(0.25f);
        color.contrast.Override(14f);
        color.saturation.Override(24f);
        var bloom = Get<Bloom>(profile);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.45f);
        bloom.scatter.Override(0.6f);
        bloom.highQualityFiltering.Override(false);
        var vig = Get<Vignette>(profile);
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.45f);
        EditorUtility.SetDirty(profile);

        var volGo = GameObject.Find("GlobalVolume");
        if (volGo == null) volGo = new GameObject("GlobalVolume");
        var vol = volGo.GetComponent<Volume>();
        if (vol == null) vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        BuildStormVolume();

        // Sky: atmosphere gradient, sun and drifting clouds. The fog uses the sky's horizon colour and
        // stays thin, so only far-off buildings fade (aerial perspective), not the whole view.
        // Cartoon palette: a clear saturated blue overhead, a bright minty-blue horizon.
        Color horizon = new Color(0.68f, 0.88f, 0.98f);
        RenderSettings.sun = light;
        RenderSettings.skybox = SkyMaterial(light, horizon);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = horizon;
        RenderSettings.fogDensity = 0.0052f;

        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            // The storm mist softens against the depth buffer.
            data.requiresDepthOption = CameraOverrideOption.On;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = horizon;
        }

        BuildSkyClouds(light);

        // A reflection probe over the arena, rendered once at start, so shiny surfaces reflect the
        // city and sky instead of the flat default.
        var probeGo = GameObject.Find("ArenaReflectionProbe");
        if (probeGo == null) probeGo = new GameObject("ArenaReflectionProbe");
        probeGo.transform.position = new Vector3(0f, 6f, 0f);
        var probe = probeGo.GetComponent<ReflectionProbe>();
        if (probe == null) probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(400f, 80f, 340f);
        probe.resolution = 128;
        probe.boxProjection = false;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.intensity = 1f;

        // The low chase camera sees detail up close: shorter shadow distance = crisper shadows.
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (urp == null) continue;
            urp.shadowDistance = 70f;
            EditorUtility.SetDirty(urp);
        }

        EditorSceneManager.MarkSceneDirty(volGo.scene);
        AssetDatabase.SaveAssets();
    }

    const float SunElevation = 46f, SunAzimuth = -40f;
    /// <summary>
    /// The sun drawn in the sky sits lower than the light that casts the shadows (same compass
    /// direction), so the low chase camera actually gets to see it; real-height shadows stay short.
    /// </summary>
    const float SkySunElevation = 16f;
    const string SkyPath = "Assets/Materials/Sky/Sky.mat";

    static Material SkyMaterial(Light light, Color horizon)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Materials/Sky");
        var shader = Shader.Find("JackpotDerby/StylizedSky");
        var m = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, SkyPath); }
        m.shader = shader;
        // Direction toward the sun: opposite a light pointing down at the sky elevation.
        m.SetVector("_SunDir", -(Quaternion.Euler(SkySunElevation, SunAzimuth, 0f) * Vector3.forward));
        m.SetColor("_HorizonColor", horizon);
        m.SetColor("_SunColor", light.color);
        // Cartoon look: soft painted bands instead of a smooth realistic gradient, no noise clouds
        // (the cloud models replace them), and a flat sun disk with a pale ring.
        m.SetColor("_ZenithColor", new Color(0.18f, 0.50f, 0.96f));
        m.SetFloat("_GradientPower", 0.7f);
        m.SetFloat("_Bands", 4f);
        m.SetFloat("_BandSoftness", 0.18f);
        m.SetFloat("_HorizonHaze", 0.35f);
        m.SetFloat("_CloudOpacity", 0f);
        m.SetFloat("_ToonSun", 1f);
        m.SetColor("_SunColor", new Color(1f, 0.96f, 0.72f));
        m.SetColor("_SunRingColor", new Color(1f, 0.97f, 0.82f));
        m.SetFloat("_SunSize", 0.0016f);
        m.SetFloat("_SunIntensity", 1.6f);
        m.SetFloat("_GlowIntensity", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Lefika's cartoon cloud model scattered round the sky: a ring of clouds just above the
    /// skyline, each drifting slowly round the map and turning to face the camera (SkyCloud).
    /// Toon-shaded and fog-free so they stay bright white against the sky.
    /// </summary>
    static void BuildSkyClouds(Light light)
    {
        var old = GameObject.Find("SkyClouds");
        if (old != null) Object.DestroyImmediate(old);
        var prefab = SceneryKit.Get("cloud") ?? SceneryKit.BuildOne("cloud");
        if (prefab == null) { Debug.LogWarning("[LookSetup] No cloud model (Assets/Models/cloud.glb)."); return; }

        ModelPrefabUtil.EnsureFolder("Assets/Materials/Sky");
        const string matPath = "Assets/Materials/Sky/SkyCloud.mat";
        var shader = Shader.Find("JackpotDerby/ToonCloud");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Scenery/Textures/cloud.png"));
        mat.SetColor("_ShadeColor", new Color(0.70f, 0.80f, 0.97f));
        EditorUtility.SetDirty(mat);

        var root = new GameObject("SkyClouds").transform;
        var rng = new System.Random(11);
        // The model is a little cluster of puffs, so a few big ones at varied heights and distances
        // look natural; many small ones look like wallpaper.
        const int count = 15;
        for (int i = 0; i < count; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.8f) / count * Mathf.PI * 2f;
            float dist = 200f + (float)rng.NextDouble() * 100f;
            // Low enough above the skyline for the low chase camera to see them.
            float height = 34f + (float)rng.NextDouble() * 46f;
            var c = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            c.name = "Cloud";
            c.transform.position = new Vector3(Mathf.Cos(a) * dist, height, Mathf.Sin(a) * dist);
            c.transform.localScale = Vector3.one * (50f + (float)rng.NextDouble() * 50f);
            var r = c.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var sc = c.AddComponent<SkyCloud>();
            sc.speed = 0.8f + (float)rng.NextDouble() * 1.2f;
        }
    }

    const string StormProfilePath = "Assets/Settings/StormVolumeProfile.asset";

    /// <summary>Gloomy grade faded in (weight 0..1) by StormController while the player is in the storm.</summary>
    static void BuildStormVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(StormProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, StormProfilePath);
        }
        var color = Get<ColorAdjustments>(profile);
        color.saturation.Override(-35f);
        color.postExposure.Override(-0.15f);
        color.colorFilter.Override(new Color(0.86f, 0.86f, 1f));
        var vig = Get<Vignette>(profile);
        vig.intensity.Override(0.38f);
        vig.smoothness.Override(0.6f);
        vig.color.Override(new Color(0.10f, 0.08f, 0.18f));
        EditorUtility.SetDirty(profile);

        var go = GameObject.Find("StormVolume");
        if (go == null) go = new GameObject("StormVolume");
        var vol = go.GetComponent<Volume>();
        if (vol == null) vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.weight = 0f;
        vol.sharedProfile = profile;
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet(out T c))
        {
            c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
        }
        c.active = true;
        return c;
    }
}
