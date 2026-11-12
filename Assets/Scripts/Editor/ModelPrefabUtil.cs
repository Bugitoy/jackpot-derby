using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared helpers for turning imported GLB models into game prefabs.</summary>
public static class ModelPrefabUtil
{
    /// <summary>
    /// Instantiate a model under parent, rotate it by yaw, scale so its longest horizontal side
    /// equals targetSize, and sit it on the parent's origin (centred, bottom at y = 0).
    /// </summary>
    public static GameObject PlaceModel(string modelPath, Transform parent, float targetSize, float yaw = 0f, string name = "Model")
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogError("[ModelPrefabUtil] Missing model " + modelPath); return null; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = name;
        inst.transform.SetParent(parent, false);
        inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

        Bounds b = LocalBounds(inst, parent);
        float s = targetSize / Mathf.Max(b.size.x, b.size.z);
        inst.transform.localScale = Vector3.one * s;
        b = LocalBounds(inst, parent);
        inst.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        return inst;
    }

    /// <summary>Renderer bounds of go, expressed in parent's local space (parent assumed unrotated/unscaled).</summary>
    public static Bounds LocalBounds(GameObject go, Transform parent)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        b.center -= parent.position;
        return b;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    /// <summary>A soft glowing, see-through material (light beams, attack circles).</summary>
    public static Material GlowMaterial(string name, Color color, Texture tex = null)
    {
        EnsureFolder("Assets/Materials/FX");
        string path = $"Assets/Materials/FX/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_Surface", 1f);     // transparent
        m.SetFloat("_Blend", 2f);       // additive
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);        // both sides
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", color);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A vertical alpha fade (bright at the bottom, gone at the top) for light beams.</summary>
    public static Texture2D BeamGradient()
    {
        const string path = "Assets/Textures/Generated/beam_gradient.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        EnsureFolder("Assets/Textures/Generated");
        var tex = new Texture2D(4, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
        {
            float v = y / 127f;
            float a = Mathf.Pow(1f - v, 1.6f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[ModelPrefabUtil] No field '{field}' on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void NoShadows(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
    }
}
