using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum GfxSetting { Auto, High, Low }

/// <summary>
/// Graphics quality for weaker machines (laptops with built-in graphics, phones).
///   High: the full look (Mesh LOD threshold 3, shadows to 50 units, bloom).
///   Low:  lighter Mesh LODs sooner, props hidden closer, shorter shadows, no bloom
///         (render scale stays 1: lowering it made the whole picture soft).
///   Auto (default): High, but drops to Low once if the first seconds of a match run under ~40 FPS
///   (and always Low on phones).
/// The choice is saved in PlayerPrefs. Created automatically; it also puts the DetailCuller on the
/// main camera. In the editor the render pipeline and volume assets are restored on exit.
/// </summary>
public class GraphicsQuality : MonoBehaviour
{
    const string PrefKey = "gfx";

    [SerializeField] float highMeshLodThreshold = 3f;
    [SerializeField] float lowMeshLodThreshold = 6f;
    [SerializeField] float highShadowDistance = 50f;
    [SerializeField] float lowShadowDistance = 30f;
    [Tooltip("Below 1 renders fewer pixels but softens the whole picture.")]
    [SerializeField] float lowRenderScale = 1f;
    [SerializeField] float lowCullScale = 0.75f;
    [Tooltip("Auto switches to Low if the match runs slower than this.")]
    [SerializeField] float autoMinFps = 40f;
    [SerializeField] float autoSettleTime = 2f;
    [SerializeField] float autoSampleTime = 4f;

    public static GraphicsQuality I { get; private set; }
    public static event Action Changed;

    public GfxSetting Setting { get; private set; }
    /// <summary>True when the Low look is in use (chosen, or picked by Auto).</summary>
    public bool LowActive { get; private set; }

    bool autoDecided;
    float autoTimer, autoFrames;

    // Original asset values, put back on exit (editor play mode edits assets in place).
    UniversalRenderPipelineAsset urp;
    float origShadow, origScale, origMeshLod;
    readonly Dictionary<Bloom, bool> blooms = new Dictionary<Bloom, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { I = null; Changed = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        var go = new GameObject("GraphicsQuality");
        DontDestroyOnLoad(go);
        go.AddComponent<GraphicsQuality>();
    }

    void Awake()
    {
        I = this;
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null) { origShadow = urp.shadowDistance; origScale = urp.renderScale; }
        origMeshLod = QualitySettings.meshLodThreshold;
        int saved = PlayerPrefs.GetInt(PrefKey, (int)GfxSetting.Auto);
        Setting = Enum.IsDefined(typeof(GfxSetting), saved) ? (GfxSetting)saved : GfxSetting.Auto;
        if (Application.isMobilePlatform) autoDecided = true;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start() => SetupScene();

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => SetupScene();

    void SetupScene()
    {
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<DetailCuller>() == null) cam.gameObject.AddComponent<DetailCuller>();
        RestoreBlooms();
        foreach (var v in FindObjectsByType<Volume>())
            if (v.sharedProfile != null && v.sharedProfile.TryGet(out Bloom b) && !blooms.ContainsKey(b)) blooms[b] = b.active;
        Apply();
    }

    /// <summary>Choose Auto, High or Low (saved).</summary>
    public void Set(GfxSetting s)
    {
        Setting = s;
        PlayerPrefs.SetInt(PrefKey, (int)s);
        PlayerPrefs.Save();
        Apply();
    }

    /// <summary>Use this setting for the rest of the session without saving it (trailer recording).</summary>
    public void Override(GfxSetting s)
    {
        Setting = s;
        autoDecided = true;
        Apply();
    }

    /// <summary>Auto → High → Low → Auto (for the menu button).</summary>
    public void Cycle() => Set(Setting == GfxSetting.Auto ? GfxSetting.High : Setting == GfxSetting.High ? GfxSetting.Low : GfxSetting.Auto);

    /// <summary>Text for the menu button, e.g. "GRAPHICS: AUTO (HIGH)".</summary>
    public string Label => Setting == GfxSetting.Auto ? $"GRAPHICS: AUTO ({(LowActive ? "LOW" : "HIGH")})" : $"GRAPHICS: {Setting.ToString().ToUpper()}";

    bool AutoWantsLow => Application.isMobilePlatform || autoLow;
    bool autoLow;

    void Apply()
    {
        LowActive = Setting == GfxSetting.Low || (Setting == GfxSetting.Auto && AutoWantsLow);
        QualitySettings.meshLodThreshold = LowActive ? lowMeshLodThreshold : highMeshLodThreshold;
        if (urp != null)
        {
            urp.shadowDistance = Mathf.Min(origShadow, LowActive ? lowShadowDistance : highShadowDistance);
            urp.renderScale = LowActive ? Mathf.Min(origScale, lowRenderScale) : origScale;
        }
        foreach (var kv in blooms) if (kv.Key != null) kv.Key.active = kv.Value && !LowActive;
        if (DetailCuller.Instance != null) DetailCuller.Instance.SetDistanceScale(LowActive ? lowCullScale : 1f);
        Changed?.Invoke();
    }

    void Update()
    {
        // Auto: watch the first seconds of real driving once per session.
        if (autoDecided || Setting != GfxSetting.Auto) return;
        var mm = MatchManager.Instance;
        if (mm == null || mm.State != MatchState.Playing || Time.timeScale <= 0f) return;
        autoTimer += Time.unscaledDeltaTime;
        if (autoTimer < autoSettleTime) return;
        autoFrames++;
        if (autoTimer < autoSettleTime + autoSampleTime) return;
        autoDecided = true;
        float fps = autoFrames / autoSampleTime;
        if (fps < autoMinFps) { autoLow = true; Apply(); }
        Debug.Log($"[GraphicsQuality] Auto measured {fps:0} FPS -> {(LowActive ? "Low" : "High")}");
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (urp != null) { urp.shadowDistance = origShadow; urp.renderScale = origScale; }
        QualitySettings.meshLodThreshold = origMeshLod;
        RestoreBlooms();
        if (I == this) I = null;
    }

    void RestoreBlooms()
    {
        foreach (var kv in blooms) if (kv.Key != null) kv.Key.active = kv.Value;
        blooms.Clear();
    }
}
