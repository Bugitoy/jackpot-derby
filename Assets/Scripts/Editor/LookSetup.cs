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

        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cam.backgroundColor = new Color(0.62f, 0.74f, 0.82f);
        }

        // Shadows only as far as the high camera needs.
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null) { urp.shadowDistance = 110f; EditorUtility.SetDirty(urp); }

        EditorSceneManager.MarkSceneDirty(volGo.scene);
        AssetDatabase.SaveAssets();
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
