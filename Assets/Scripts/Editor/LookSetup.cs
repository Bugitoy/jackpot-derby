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
        light.transform.rotation = Quaternion.Euler(52f, -40f, 0f);
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

        // Soft haze: the city fades into the sky colour instead of ending at a hard edge.
        Color sky = new Color(0.66f, 0.76f, 0.84f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = sky;
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 165f;

        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            // The storm mist softens against the depth buffer.
            data.requiresDepthOption = CameraOverrideOption.On;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
        }

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
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = sky;
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
