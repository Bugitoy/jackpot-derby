using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Shows a real 3D model inside the UI (How to Play icons, the results trophy): the model sits on its
/// own little stage far below the arena on the ModelIcon layer, a private camera renders it into a
/// RenderTexture, and a RawImage shows that. It only renders while the UI element is active.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ModelViewport : MonoBehaviour
{
    public const string LayerName = "ModelIcon";
    static int nextSlot;

    Camera vcam;
    RenderTexture rt;
    Transform stage, spinner;
    float spinSpeed, bob;
    float age;

    /// <summary>
    /// Makes a RawImage under parent showing model. yaw = starting turn, spin = degrees per second,
    /// pitch = how far the camera looks down on it, zoom &gt; 1 frames it tighter.
    /// </summary>
    public static RawImage Create(RectTransform parent, GameObject model, Vector2 size, float yaw = 30f,
                                  float spin = 25f, float pitch = 14f, float zoom = 1f, float bobAmount = 0.03f)
    {
        var go = new GameObject("Model_" + (model != null ? model.name : "none"), typeof(RectTransform), typeof(RawImage));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.sizeDelta = size;
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        if (model == null) { img.color = Color.clear; return img; }
        var mv = go.AddComponent<ModelViewport>();
        mv.Init(model, size, yaw, spin, pitch, zoom, bobAmount);
        return img;
    }

    void Init(GameObject model, Vector2 size, float yaw, float spin, float pitch, float zoom, float bobAmount)
    {
        spinSpeed = spin;
        bob = bobAmount;
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0) layer = 31;

        Vector3 origin = new Vector3(-2000f + (nextSlot++ % 64) * 12f, -1000f, 0f);
        stage = new GameObject("ViewportStage_" + model.name).transform;
        stage.position = origin;
        spinner = new GameObject("Spinner").transform;
        spinner.SetParent(stage, false);
        spinner.localRotation = Quaternion.Euler(0f, yaw, 0f);

        var inst = Instantiate(model, spinner);
        inst.name = model.name;
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var b in inst.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        SetLayer(inst.transform, layer);

        // Normalize: biggest side = 1 unit, centred on the spinner (visible parts only: e.g. the
        // clover pickup keeps its coin base switched off).
        var rends = inst.GetComponentsInChildren<Renderer>(false);
        if (rends.Length > 0)
        {
            Bounds bb = rends[0].bounds;
            foreach (var rr in rends) bb.Encapsulate(rr.bounds);
            float s = 1f / Mathf.Max(0.001f, Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z)));
            inst.transform.localScale *= s;
            bb = rends[0].bounds;
            foreach (var rr in rends) { bb.Encapsulate(rr.bounds); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            inst.transform.position += spinner.position - bb.center;
        }

        int px = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(Mathf.Max(size.x, size.y) * 1.5f));
        px = Mathf.Clamp(px, 128, 512);
        rt = new RenderTexture(px, Mathf.RoundToInt(px * size.y / Mathf.Max(1f, size.x)), 24, RenderTextureFormat.ARGB32)
        { antiAliasing = 4, name = "ModelViewportRT" };

        vcam = new GameObject("ViewportCam").AddComponent<Camera>();
        vcam.transform.SetParent(stage, false);
        vcam.fieldOfView = 24f;
        float dist = 0.62f / Mathf.Tan(vcam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.2f, zoom);
        Quaternion look = Quaternion.Euler(pitch, 0f, 0f);
        vcam.transform.localPosition = look * new Vector3(0f, 0f, -dist);
        vcam.transform.localRotation = look;
        vcam.nearClipPlane = 0.05f;
        vcam.farClipPlane = dist + 3f;
        vcam.clearFlags = CameraClearFlags.SolidColor;
        vcam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        vcam.cullingMask = 1 << layer;
        vcam.targetTexture = rt;
        vcam.allowHDR = false;
        vcam.depth = -10;
        var data = vcam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.antialiasing = AntialiasingMode.None;

        // Warm key light and a cool rim so the hand-painted models read nicely on the dark cards.
        AddLight(new Vector3(-1.4f, 1.8f, -2.2f), new Color(1f, 0.92f, 0.8f), 3.2f, layer);
        AddLight(new Vector3(1.8f, 1.2f, 1.6f), new Color(0.6f, 0.9f, 1f), 2.4f, layer);

        GetComponent<RawImage>().texture = rt;
        if (!isActiveAndEnabled) vcam.enabled = false;
    }

    void AddLight(Vector3 localPos, Color color, float intensity, int layer)
    {
        var l = new GameObject("ViewportLight").AddComponent<Light>();
        l.transform.SetParent(stage, false);
        l.transform.localPosition = localPos;
        l.type = LightType.Point;
        l.range = 7f;
        l.color = color;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.cullingMask = 1 << layer;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }

    void OnEnable() { if (vcam != null) vcam.enabled = true; age = 0f; }
    void OnDisable() { if (vcam != null) vcam.enabled = false; }

    void Update()
    {
        if (spinner == null) return;
        float dt = Time.unscaledDeltaTime;
        age += dt;
        spinner.Rotate(0f, spinSpeed * dt, 0f, Space.Self);
        spinner.localPosition = new Vector3(0f, bob * Mathf.Sin(age * 2.2f), 0f);
    }

    void OnDestroy()
    {
        if (stage != null) Destroy(stage.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }
}
