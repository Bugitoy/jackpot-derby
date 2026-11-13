using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns the Tripo GLB models into scenery prefabs at full quality: the pieces are merged into one
/// mesh (one submesh per source material, so multi-material models like the slot machine keep every
/// texture), scaled to game size (centred, bottom at y = 0, front facing +Z), with copies of the
/// model's own glTF materials and one box collider.
///
/// Tripo exports two kinds of material:
///  * base colour only (metallic 0): copied as-is.
///  * PBR (normal + metallicRoughness maps, no factors, so glTF's default metallic = 1): the maps are
///    kept, but metallic is forced to 0. Fully metallic props render almost black under our lighting.
/// Menu: Jackpot Derby > Build Scenery Prefabs.
/// </summary>
public static class SceneryKit
{
    public const string PrefabFolder = "Assets/Prefabs/Scenery";
    const string MeshFolder = "Assets/Scenery/Meshes";
    const string TexFolder = "Assets/Scenery/Textures";
    const string MatFolder = "Assets/Scenery/Materials";

    public enum Col { None, Box }

    public struct Spec
    {
        public string name;   // model file name in Assets/Models (without .glb)
        public float size;    // longest horizontal side in units
        public Col col;
        public float colScale; // collider footprint scale (1 = full bounds)
        public float yaw;     // rotation baked in so the model's front faces +Z
        public bool glow;     // neon: bright saturated colours become emissive
        public Spec(string n, float s, Col c = Col.Box, float cs = 1f, float y = 0f, bool glow = false)
        { name = n; size = s; col = c; colScale = cs; yaw = y; this.glow = glow; }
    }

    // Yaw values set from a screenshot lineup so every front faces +Z.
    public static readonly Spec[] Specs =
    {
        // Buildings
        new Spec("apartment_brick",    8.5f),
        new Spec("apartment_cream",    8.5f),
        new Spec("apartment_cream_2",  8.5f),
        new Spec("apartment_teal",     8.5f),
        new Spec("arcade_building",    9.0f),
        new Spec("casino",            10.5f),
        new Spec("charm_shop",         8.0f),
        new Spec("house_small",        7.5f),
        new Spec("house_small_yellow", 7.5f),
        new Spec("house_tall",         7.0f),
        new Spec("store_cafe",         8.5f),
        new Spec("store_grocer",       8.5f),
        new Spec("store_laundromat",   8.5f),
        new Spec("store_mechanic",     8.5f),
        new Spec("store_pawn",         8.5f),
        new Spec("garage_small",       8.0f),
        // Corner garages
        new Spec("garage_lucky7",       10.5f),
        new Spec("garage_lucky8",       10.5f),
        new Spec("garage_blackcat13",   10.5f),
        new Spec("garage_doubledice11", 10.5f),
        // Edge + road kit
        new Spec("arena_wall",      6.0f),
        new Spec("retaining_wall",  6.0f),
        new Spec("guardrail",       4.0f),
        new Spec("overpass_pillar", 3.0f),
        new Spec("tunnel_entrance", 12.0f),
        new Spec("finish_arch",     12.0f, Col.None),
        // Rooftop dressing (no colliders: up on roofs)
        new Spec("roof_ac_unit",     1.8f, Col.None),
        new Spec("roof_antennas",    2.2f, Col.None),
        new Spec("roof_billboard",   4.5f, Col.None),
        new Spec("roof_vents",       1.8f, Col.None),
        new Spec("roof_water_tower", 2.6f, Col.None),
        new Spec("neon_sign_clover", 2.8f, Col.None, glow: true),
        new Spec("neon_sign_dice",   2.8f, Col.None, glow: true),
        // Parked vehicles
        new Spec("parked_sedan", 4.2f),
        new Spec("parked_van",   4.6f),
        new Spec("taxi",         4.2f),
        new Spec("city_bus",     9.0f),
        // Street props
        new Spec("barrier",         2.6f),
        new Spec("bus_stop",        3.6f),
        new Spec("bush_large",      2.2f, Col.None),
        new Spec("cone",            0.6f, Col.None),
        new Spec("dumpster",        2.4f),
        new Spec("fence_wood",      4.0f),
        new Spec("fence_wood_2",    4.0f),
        new Spec("fire_hydrant",    0.6f),
        new Spec("mailbox",         0.8f),
        new Spec("metal_drums",     1.6f),
        new Spec("oil_barrel",      0.9f),
        new Spec("pallet_stack",    1.8f),
        new Spec("park_bench",      2.2f),
        new Spec("pole_concrete",   0.8f, Col.Box, 0.5f),
        new Spec("pole_wood",       1.0f, Col.Box, 0.4f),
        new Spec("potted_plants",   1.6f),
        new Spec("street_lamp",     0.9f, Col.Box, 0.4f),
        new Spec("traffic_light",   1.4f, Col.Box, 0.3f),
        new Spec("transformer_box", 1.6f),
        new Spec("trash_bags",      1.4f, Col.None),
        new Spec("tree_planter",    3.2f, Col.Box, 0.35f),
        new Spec("tree_twisted",    3.6f, Col.Box, 0.25f),
        new Spec("vending_machine", 1.2f),
        new Spec("slot_machine",    1.4f),
        new Spec("coin",            1.0f, Col.None),
        new Spec("wheel",           0.98f, Col.None), // Lefika's car wheel (replaces the Tripo cars' gappy wheels)
        new Spec("cloud",           1.0f, Col.None),  // Lefika's cartoon cloud (sky dressing, see LookSetup)
        new Spec("road_slab_lucky", 6.0f, Col.None),
    };

    /// <summary>Optional models Lefika may add later; built only when the GLB exists.</summary>
    public static readonly Spec[] OptionalSpecs =
    {
        new Spec("grass_tuft", 1.2f, Col.None),
    };

    public static GameObject Get(string name)
        => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");

    static IEnumerable<Spec> AllSpecs()
    {
        foreach (var s in Specs) yield return s;
        foreach (var s in OptionalSpecs)
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/{s.name}.glb") != null) yield return s;
    }

    [MenuItem("Jackpot Derby/Build Scenery Prefabs")]
    public static void BuildAll()
    {
        foreach (var f in new[] { PrefabFolder, MeshFolder, TexFolder, MatFolder }) ModelPrefabUtil.EnsureFolder(f);
        var specs = new List<Spec>(AllSpecs());
        var written = new List<(string path, TexKind kind)>();
        int done = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var s in specs)
            {
                EditorUtility.DisplayProgressBar("Scenery textures", s.name, done++ / (float)specs.Count);
                var model = LoadModel(s.name);
                if (model == null) continue;
                var mats = SourceMaterials(model);
                for (int i = 0; i < mats.Count; i++)
                    written.AddRange(WriteMaterialTextures(mats[i], $"{TexFolder}/{SlotName(s.name, i)}", s.glow, false));
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        done = 0;
        foreach (var w in written)
        {
            EditorUtility.DisplayProgressBar("Texture import", w.path, done++ / (float)written.Count);
            ConfigureTexture(w.path, w.kind);
        }
        done = 0;
        foreach (var s in specs)
        {
            EditorUtility.DisplayProgressBar("Scenery prefabs", s.name, done++ / (float)specs.Count);
            Build(s);
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneryKit] Built {specs.Count} scenery prefabs ({written.Count} textures).");
    }

    /// <summary>Build (or rebuild) just one scenery prefab, e.g. after adding a single new model.</summary>
    public static GameObject BuildOne(string name)
    {
        foreach (var f in new[] { PrefabFolder, MeshFolder, TexFolder, MatFolder }) ModelPrefabUtil.EnsureFolder(f);
        foreach (var s in AllSpecs())
        {
            if (s.name != name) continue;
            var model = LoadModel(s.name);
            if (model == null) return null;
            var mats = SourceMaterials(model);
            for (int i = 0; i < mats.Count; i++)
                WriteMaterialTextures(mats[i], $"{TexFolder}/{SlotName(s.name, i)}", s.glow, true);
            Build(s);
            AssetDatabase.SaveAssets();
            return Get(name);
        }
        Debug.LogWarning("[SceneryKit] No spec named " + name);
        return null;
    }

    static GameObject LoadModel(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/{name}.glb");

    /// <summary>Slot 0 keeps the old file names so existing references stay valid.</summary>
    static string SlotName(string model, int slot) => slot == 0 ? model : $"{model}_{slot}";

    static void Build(Spec s)
    {
        var model = LoadModel(s.name);
        if (model == null) { Debug.LogWarning("[SceneryKit] Missing model Assets/Models/" + s.name + ".glb"); return; }

        var srcMats = SourceMaterials(model);
        bool needsTangents = false;
        foreach (var m in srcMats) needsTangents |= HasTex(m, "normalTexture");

        var mesh = BuildMesh(model, s, srcMats, needsTangents);
        string meshPath = $"{MeshFolder}/{s.name}.asset";
        var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (oldMesh != null) { EditorUtility.CopySerialized(mesh, oldMesh); Object.DestroyImmediate(mesh); mesh = oldMesh; }
        else AssetDatabase.CreateAsset(mesh, meshPath);

        var mats = new Material[srcMats.Count];
        for (int i = 0; i < srcMats.Count; i++)
            mats[i] = CopyMaterial(srcMats[i], $"{TexFolder}/{SlotName(s.name, i)}", $"{MatFolder}/{SlotName(s.name, i)}.mat", s.glow);

        var root = new GameObject(s.name);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        // Small street props don't need to cast shadows (saves a whole extra draw of their mesh).
        mr.shadowCastingMode = s.size < 2.5f ? ShadowCastingMode.Off : ShadowCastingMode.On;
        mr.receiveShadows = true;
        if (s.col == Col.Box)
        {
            var b = mesh.bounds;
            var bc = root.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = new Vector3(b.size.x * s.colScale, b.size.y, b.size.z * s.colScale);
        }
        // Not batching-static: hundreds of copies would bloat the WebGL download; the SRP Batcher
        // keeps these shared meshes cheap to draw instead.
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{s.name}.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>Distinct materials of a model, in the order they first appear.</summary>
    static List<Material> SourceMaterials(GameObject model)
    {
        var list = new List<Material>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null && !list.Contains(m)) list.Add(m);
        return list;
    }

    /// <summary>Merge every piece into one mesh with one submesh per material, then normalise it.</summary>
    static Mesh BuildMesh(GameObject model, Spec s, List<Material> slots, bool tangents)
    {
        var inst = (GameObject)Object.Instantiate(model);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.transform.localScale = Vector3.one;
        var groups = new List<CombineInstance>[Mathf.Max(1, slots.Count)];
        for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var rend = mf.GetComponent<Renderer>();
            var rm = rend != null ? rend.sharedMaterials : new Material[0];
            for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
            {
                var mat = rm.Length > 0 ? rm[Mathf.Min(sub, rm.Length - 1)] : null;
                int slot = Mathf.Max(0, slots.IndexOf(mat));
                groups[slot].Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sub, transform = mf.transform.localToWorldMatrix });
            }
        }
        Object.DestroyImmediate(inst);

        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
        var subIdx = new List<int[]>();
        foreach (var g in groups)
        {
            if (g.Count == 0) { subIdx.Add(new int[0]); continue; }
            var part = new Mesh { indexFormat = IndexFormat.UInt32 };
            part.CombineMeshes(g.ToArray(), true, true);
            int baseV = v.Count;
            var pv = part.vertices; var pn = part.normals; var puv = part.uv;
            for (int i = 0; i < pv.Length; i++)
            {
                v.Add(pv[i]);
                n.Add(pn.Length > i ? pn[i] : Vector3.up);
                uv.Add(puv.Length > i ? puv[i] : Vector2.zero);
            }
            var idx = part.GetIndices(0);
            for (int i = 0; i < idx.Length; i++) idx[i] += baseV;
            subIdx.Add(idx);
            Object.DestroyImmediate(part);
        }

        // Normalise: yaw, scale to target size, centre on XZ, bottom on y = 0.
        var rot = Quaternion.Euler(0f, s.yaw, 0f);
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < v.Count; i++)
        {
            v[i] = rot * v[i]; n[i] = rot * n[i];
            min = Vector3.Min(min, v[i]); max = Vector3.Max(max, v[i]);
        }
        Vector3 size = max - min;
        float scale = s.size / Mathf.Max(size.x, size.z);
        Vector3 offset = new Vector3(-(min.x + max.x) * 0.5f, -min.y, -(min.z + max.z) * 0.5f);
        for (int i = 0; i < v.Count; i++) v[i] = (v[i] + offset) * scale;

        var mesh = new Mesh { name = s.name };
        mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
        mesh.subMeshCount = subIdx.Count;
        for (int i = 0; i < subIdx.Count; i++) mesh.SetTriangles(subIdx[i], i);
        mesh.RecalculateBounds();
        if (tangents) mesh.RecalculateTangents();
        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Off);
        return mesh;
    }

    // ---------- textures ----------

    enum TexKind { Color, Normal, Linear }

    static bool HasTex(Material m, string prop) => m != null && m.HasProperty(prop) && m.GetTexture(prop) != null;

    static Texture BaseTexture(Material m)
    {
        if (m == null) return null;
        foreach (var p in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
            if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
        foreach (var p in m.GetTexturePropertyNames())
            if (m.GetTexture(p) is Texture2D t) return t;
        return null;
    }

    /// <summary>
    /// Write PNG copies of a material's base colour, normal and metallicRoughness maps (plus an emission
    /// mask for neon). glTFast keeps GLB textures uncompressed (~17 MB at 2K); the copies get proper
    /// compression. Returns the files written.
    /// </summary>
    static List<(string, TexKind)> WriteMaterialTextures(Material m, string basePath, bool glow, bool importNow)
    {
        var list = new List<(string, TexKind)>();
        var baseTex = BaseTexture(m);
        if (baseTex != null) { WritePng(baseTex, basePath + ".png", false, null, importNow); list.Add((basePath + ".png", TexKind.Color)); }
        if (HasTex(m, "normalTexture")) { WritePng(m.GetTexture("normalTexture"), basePath + "_n.png", true, null, importNow); list.Add((basePath + "_n.png", TexKind.Normal)); }
        if (HasTex(m, "metallicRoughnessTexture")) { WritePng(m.GetTexture("metallicRoughnessTexture"), basePath + "_mr.png", true, null, importNow); list.Add((basePath + "_mr.png", TexKind.Linear)); }
        if (glow && baseTex != null) { WritePng(baseTex, basePath + "_e.png", false, NeonMask, importNow); list.Add((basePath + "_e.png", TexKind.Color)); }
        return list;
    }

    /// <summary>Keep only the bright, saturated "tube" colours of a neon sign; everything else black.</summary>
    static Color NeonMask(Color c)
    {
        Color.RGBToHSV(c, out _, out float sat, out float val);
        float k = Mathf.Clamp01((sat - 0.45f) / 0.2f) * Mathf.Clamp01((val - 0.6f) / 0.2f);
        return new Color(c.r * k, c.g * k, c.b * k, 1f);
    }

    /// <summary>Copy a texture to a PNG at its own resolution (2K max), optionally filtering the pixels.</summary>
    static void WritePng(Texture src, string path, bool linear, System.Func<Color, Color> filter, bool importNow)
    {
        int w = Mathf.Min(src.width, 2048), h = Mathf.Min(src.height, 2048);
        var rw = linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, rw);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false, linear);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        if (filter != null)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = filter(px[i]);
            tex.SetPixels(px);
            tex.Apply();
        }
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        if (importNow) ConfigureTexture(path, path.EndsWith("_n.png") ? TexKind.Normal : path.EndsWith("_mr.png") ? TexKind.Linear : TexKind.Color);
    }

    /// <summary>
    /// Full quality: source resolution, high-quality block compression, no crunch (crunch smeared the
    /// hand-painted detail).
    /// </summary>
    static void ConfigureTexture(string path, TexKind kind)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        // Normal maps stay plain linear RGB textures, exactly like glTFast imports them: the glTF shader
        // unpacks raw RGB. Importing them as Unity "Normal map" re-encodes them (DXTnm) and the shader
        // then misreads them, which showed up as blotchy patches on every PBR model.
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = kind == TexKind.Color;
        imp.maxTextureSize = 2048;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.crunchedCompression = false;
        imp.compressionQuality = 100;
        imp.SaveAndReimport();
    }

    // ---------- materials ----------

    /// <summary>
    /// A copy of a model's own glTF material (same shader and settings, so it looks like the original),
    /// pointing at the PNG copies of its textures. PBR materials get metallic 0 (see class summary).
    /// </summary>
    static Material CopyMaterial(Material src, string texBase, string matPath, bool glow)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, matPath); }
        else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); m.shaderKeywords = src.shaderKeywords; }

        foreach (var p in m.GetTexturePropertyNames())
            if (m.GetTexture(p) != null && AssetDatabase.GetAssetPath(m.GetTexture(p)).EndsWith(".glb")) m.SetTexture(p, null);

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + ".png");
        if (m.HasProperty("baseColorTexture")) m.SetTexture("baseColorTexture", baseTex);
        else if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex);

        bool pbr = HasTex(src, "metallicRoughnessTexture") || HasTex(src, "normalTexture");
        if (HasTex(src, "normalTexture") && m.HasProperty("normalTexture"))
            m.SetTexture("normalTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_n.png"));
        if (HasTex(src, "metallicRoughnessTexture") && m.HasProperty("metallicRoughnessTexture"))
            m.SetTexture("metallicRoughnessTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_mr.png"));
        if (pbr && m.HasProperty("metallicFactor")) m.SetFloat("metallicFactor", 0f);

        if (glow && m.HasProperty("emissiveTexture"))
        {
            m.SetTexture("emissiveTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_e.png"));
            m.SetColor("emissiveFactor", Color.white * 2.2f);
            m.EnableKeyword("_EMISSIVE");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Point the gameplay prefabs (cars, blasters, chests, pickups) at full-quality copies of their glTF
    /// materials: one copy per source material (multi-part models like the die keep every texture),
    /// with the same metallic fix as the scenery. Same glTF shader, so CarFx flashes still work.
    /// Run it right after the prefab builders (they reset the GLB materials).
    /// </summary>
    [MenuItem("Jackpot Derby/Compress Gameplay Model Textures")]
    public static void CompressGameplayTextures()
    {
        foreach (var f in new[] { TexFolder, MatFolder }) ModelPrefabUtil.EnsureFolder(f);
        var swaps = new Dictionary<Material, Material>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cars", "Assets/Prefabs/Combat", "Assets/Prefabs/Pickups" });
        foreach (var g in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                bool rChanged = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i];
                    if (m == null) continue;
                    string src = AssetDatabase.GetAssetPath(m);
                    if (!src.EndsWith(".glb")) continue;
                    if (!swaps.TryGetValue(m, out var copy))
                    {
                        string model = System.IO.Path.GetFileNameWithoutExtension(src).Replace(" ", "_");
                        int slot = SourceMaterials(AssetDatabase.LoadAssetAtPath<GameObject>(src)).IndexOf(m);
                        string baseName = $"GP_{SlotName(model, Mathf.Max(0, slot))}";
                        WriteMaterialTextures(m, $"{TexFolder}/{baseName}", false, true);
                        copy = CopyMaterial(m, $"{TexFolder}/{baseName}", $"{MatFolder}/{baseName}.mat", false);
                        swaps[m] = copy;
                    }
                    ms[i] = copy;
                    rChanged = true;
                }
                if (rChanged) { r.sharedMaterials = ms; changed = true; }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneryKit] Gameplay prefabs now use {swaps.Count} full-quality materials.");
    }
}
