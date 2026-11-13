using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns the heavy Tripo GLB models (~37k triangles in ~38 pieces, 2K texture each) into light
/// scenery prefabs for the WebGL arena: all pieces merged into one mesh, decimated with Unity's
/// mesh LOD generator to a triangle budget, scaled to game size (centred, bottom at y = 0,
/// front facing +Z), with a downscaled texture on a Simple Lit material and one box collider.
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
        public int tris;      // triangle budget
        public int tex;       // texture size
        public Col col;
        public float colScale; // collider footprint scale (1 = full bounds)
        public float yaw;     // rotation baked in so the model's front faces +Z
        public Spec(string n, float s, int t, int x, Col c = Col.Box, float cs = 1f, float y = 0f)
        { name = n; size = s; tris = t; tex = x; col = c; colScale = cs; yaw = y; }
    }

    // Yaw values set from a screenshot lineup so every front faces +Z.
    public static readonly Spec[] Specs =
    {
        // Buildings
        new Spec("apartment_brick",    8.5f, 12000, 1024),
        new Spec("apartment_cream",    8.5f, 12000, 1024),
        new Spec("apartment_cream_2",  8.5f, 12000, 1024),
        new Spec("apartment_teal",     8.5f, 12000, 1024),
        new Spec("arcade_building",    9.0f, 12000, 1024),
        new Spec("casino",            10.5f, 12000, 1024),
        new Spec("charm_shop",         8.0f, 12000, 1024),
        new Spec("house_small",        7.5f, 12000, 1024),
        new Spec("house_small_yellow", 7.5f, 12000, 1024),
        new Spec("house_tall",         7.0f, 12000, 1024),
        new Spec("store_cafe",         8.5f, 12000, 1024),
        new Spec("store_grocer",       8.5f, 12000, 1024),
        new Spec("store_laundromat",   8.5f, 12000, 1024),
        new Spec("store_mechanic",     8.5f, 12000, 1024),
        new Spec("store_pawn",         8.5f, 12000, 1024),
        new Spec("garage_small",       8.0f, 12000, 1024),
        // Corner garages
        new Spec("garage_lucky7",       10.5f, 20000, 1024),
        new Spec("garage_lucky8",       10.5f, 20000, 1024),
        new Spec("garage_blackcat13",   10.5f, 20000, 1024),
        new Spec("garage_doubledice11", 10.5f, 20000, 1024),
        // Edge + road kit
        new Spec("arena_wall",      6.0f, 3000, 512),
        new Spec("retaining_wall",  6.0f, 3000, 512),
        new Spec("guardrail",       4.0f, 3000, 512),
        new Spec("overpass_pillar", 3.0f, 3000, 512),
        new Spec("tunnel_entrance", 12.0f, 8000, 1024),
        new Spec("finish_arch",     12.0f, 3000, 1024, Col.None),
        // Rooftop dressing (no colliders: up on roofs)
        new Spec("roof_ac_unit",     1.8f, 3000, 256, Col.None),
        new Spec("roof_antennas",    2.2f, 3000, 256, Col.None),
        new Spec("roof_billboard",   4.5f, 3000, 512, Col.None),
        new Spec("roof_vents",       1.8f, 3000, 256, Col.None),
        new Spec("roof_water_tower", 2.6f, 3000, 512, Col.None),
        new Spec("neon_sign_clover", 2.8f, 3000, 512, Col.None),
        new Spec("neon_sign_dice",   2.8f, 3000, 512, Col.None),
        // Parked vehicles
        new Spec("parked_sedan", 4.2f, 6000, 512),
        new Spec("parked_van",   4.6f, 6000, 512),
        new Spec("taxi",         4.2f, 6000, 512),
        new Spec("city_bus",     9.0f, 6000, 512),
        // Street props
        new Spec("barrier",         2.6f, 3000, 512),
        new Spec("bus_stop",        3.6f, 3000, 512),
        new Spec("bush_large",      2.2f, 6000, 512, Col.None),
        new Spec("cone",            0.6f, 3000,  256, Col.None),
        new Spec("dumpster",        2.4f, 3000, 512),
        new Spec("fence_wood",      4.0f, 3000, 512, Col.Box, 1f),
        new Spec("fence_wood_2",    4.0f, 3000, 512, Col.Box, 1f),
        new Spec("fire_hydrant",    0.6f, 3000,  256),
        new Spec("mailbox",         0.8f, 3000,  256),
        new Spec("metal_drums",     1.6f, 3000, 512),
        new Spec("oil_barrel",      0.9f, 3000,  256),
        new Spec("pallet_stack",    1.8f, 3000, 512),
        new Spec("park_bench",      2.2f, 3000, 512),
        new Spec("pole_concrete",   0.8f, 3000,  256, Col.Box, 0.5f),
        new Spec("pole_wood",       1.0f, 3000,  256, Col.Box, 0.4f),
        new Spec("potted_plants",   1.6f, 3000, 512),
        new Spec("street_lamp",     0.9f, 3000,  256, Col.Box, 0.4f),
        new Spec("traffic_light",   1.4f, 3000,  256, Col.Box, 0.3f),
        new Spec("transformer_box", 1.6f, 3000, 512),
        new Spec("trash_bags",      1.4f, 3000,  256, Col.None),
        new Spec("tree_planter",    3.2f, 6000, 512, Col.Box, 0.35f),
        new Spec("tree_twisted",    3.6f, 6000, 512, Col.Box, 0.25f),
        new Spec("vending_machine", 1.2f, 3000,  256),
        new Spec("slot_machine",    1.4f, 12000, 1024),
        new Spec("coin",            1.0f, 3000,  256, Col.None),
        new Spec("road_slab_lucky", 6.0f, 3000, 512, Col.None),
    };

    /// <summary>True: original meshes and full-size textures (crunch-compressed for download size).</summary>
    public static bool FullQuality = true;

    public static GameObject Get(string name)
        => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");

    [MenuItem("Jackpot Derby/Build Scenery Prefabs")]
    public static void BuildAll()
    {
        foreach (var f in new[] { PrefabFolder, MeshFolder, TexFolder, MatFolder }) ModelPrefabUtil.EnsureFolder(f);
        int done = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var s in Specs)
            {
                EditorUtility.DisplayProgressBar("Scenery prefabs", s.name, done++ / (float)Specs.Length);
                BuildTexture(s);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        foreach (var s in Specs) ConfigureTexture(s);
        done = 0;
        foreach (var s in Specs)
        {
            EditorUtility.DisplayProgressBar("Scenery prefabs", s.name, done++ / (float)Specs.Length);
            Build(s);
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneryKit] Built {Specs.Length} scenery prefabs.");
    }

    static void Build(Spec s)
    {
        string modelPath = $"Assets/Models/{s.name}.glb";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogWarning("[SceneryKit] Missing model " + modelPath); return; }

        var mesh = BuildMesh(model, s);
        string meshPath = $"{MeshFolder}/{s.name}.asset";
        var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (oldMesh != null) { EditorUtility.CopySerialized(mesh, oldMesh); Object.DestroyImmediate(mesh); mesh = oldMesh; }
        else AssetDatabase.CreateAsset(mesh, meshPath);

        var mat = BuildMaterial(s, model);

        var root = new GameObject(s.name);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        // Small street props don't need to cast shadows (saves a whole extra draw of their mesh).
        mr.shadowCastingMode = s.size < 2.5f ? ShadowCastingMode.Off : ShadowCastingMode.On;
        mr.receiveShadows = true;
        if (s.col == Col.Box)
        {
            var b = mesh.bounds;
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(b.center.x, b.center.y, b.center.z);
            bc.size = new Vector3(b.size.x * s.colScale, b.size.y, b.size.z * s.colScale);
        }
        // Not batching-static: hundreds of copies would bloat the WebGL download; the SRP Batcher
        // keeps these shared meshes cheap to draw instead.
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{s.name}.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>Merge every piece into one mesh, normalise it, then keep the LOD nearest the budget.</summary>
    static Mesh BuildMesh(GameObject model, Spec s)
    {
        var inst = (GameObject)Object.Instantiate(model);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.transform.localScale = Vector3.one;
        var cis = new List<CombineInstance>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                cis.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sub, transform = mf.transform.localToWorldMatrix });
        }
        Object.DestroyImmediate(inst);

        var full = new Mesh { indexFormat = IndexFormat.UInt32 };
        full.CombineMeshes(cis.ToArray(), true, true);

        // Full detail by default (the decimated versions smeared the hand-painted textures).
        // With FullQuality off, keep the most detailed generated LOD that fits the triangle budget.
        uint pickStart = 0, pickCount = full.GetIndexCount(0);
        if (!FullQuality)
        {
            MeshLodUtility.GenerateMeshLods(full, 10);
            var lods = full.GetLods(0);
            var pick = lods[lods.Length - 1];
            foreach (var l in lods) if (l.indexCount / 3 <= s.tris) { pick = l; break; }
            pickStart = pick.indexStart; pickCount = pick.indexCount;
        }

        var srcV = full.vertices; var srcN = full.normals; var srcUv = full.uv;
        int[] allIdx;
        using (var data = Mesh.AcquireReadOnlyMeshData(full))
            allIdx = data[0].GetIndexData<int>().ToArray();
        Object.DestroyImmediate(full);

        // Compact: keep only vertices the chosen LOD uses.
        var remap = new Dictionary<int, int>();
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
        var idx = new int[pickCount];
        for (int i = 0; i < pickCount; i++)
        {
            int o = allIdx[pickStart + i];
            if (!remap.TryGetValue(o, out int ni))
            {
                ni = v.Count; remap[o] = ni;
                v.Add(srcV[o]);
                n.Add(srcN.Length > o ? srcN[o] : Vector3.up);
                uv.Add(srcUv.Length > o ? srcUv[o] : Vector2.zero);
            }
            idx[i] = ni;
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
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateBounds();
        // Quantise vertex data in the build: smaller download, no visible difference at this size.
        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
        return mesh;
    }

    static Texture SourceTexture(Material m, out Color tint)
    {
        tint = Color.white;
        if (m == null) return null;
        foreach (var c in new[] { "baseColorFactor", "_BaseColor", "_Color" })
            if (m.HasProperty(c)) { tint = m.GetColor(c); break; }
        foreach (var p in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
            if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
        foreach (var p in m.GetTexturePropertyNames())
            if (m.GetTexture(p) is Texture2D t) return t;
        return null;
    }

    static Material FirstMaterial(GameObject model)
    {
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial != null) return r.sharedMaterial;
        return null;
    }

    static string TexPath(Spec s) => $"{TexFolder}/{s.name}.png";

    static void BuildTexture(Spec s)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/{s.name}.glb");
        if (model == null) return;
        var src = SourceTexture(FirstMaterial(model), out _);
        if (src == null) return;
        WritePng(src, TexPath(s), s.tex);
    }

    /// <summary>Full quality keeps the source resolution (2K max); otherwise scales down to size.</summary>
    static void WritePng(Texture src, string path, int size)
    {
        int w = Mathf.Min(size, src.width), h = Mathf.Min(size, src.height);
        if (FullQuality) { w = Mathf.Min(src.width, 2048); h = Mathf.Min(src.height, 2048); }
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
    }

    static void ConfigureTexture(Spec s) => ConfigureTexture(TexPath(s), FullQuality ? 2048 : s.tex);

    static void ConfigureTexture(string path, int size)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.maxTextureSize = size;
        imp.sRGBTexture = true;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        // DXT + crunch at high quality: looks like the original but downloads at a fraction of the size.
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true;
        imp.compressionQuality = 90;
        imp.SaveAndReimport();
    }

    /// <summary>
    /// glTFast keeps each GLB texture uncompressed (~17 MB at 2K), which bloats the WebGL download.
    /// Point the gameplay prefabs (cars, blasters, chests, pickups) at copies of their materials
    /// that use the same full-size texture, crunch-compressed. Same glTF shader, so CarFx flashes
    /// still work. Run it right after the prefab builders (they reset the GLB materials).
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
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i];
                    if (m == null) continue;
                    string src = AssetDatabase.GetAssetPath(m);
                    if (!src.EndsWith(".glb")) continue;
                    if (!swaps.TryGetValue(m, out var copy))
                    {
                        string model = System.IO.Path.GetFileNameWithoutExtension(src).Replace(" ", "_");
                        string texPath = $"{TexFolder}/GP_{model}.png";
                        string matPath = $"{MatFolder}/GP_{model}.mat";
                        var tex = SourceTexture(m, out _);
                        if (tex != null) { WritePng(tex, texPath, 2048); ConfigureTexture(texPath, 2048); }
                        copy = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (copy == null) { copy = new Material(m); AssetDatabase.CreateAsset(copy, matPath); }
                        else copy.CopyPropertiesFromMaterial(m);
                        foreach (var p in copy.GetTexturePropertyNames())
                            if (copy.GetTexture(p) != null && AssetDatabase.GetAssetPath(copy.GetTexture(p)).EndsWith(".glb")) copy.SetTexture(p, null);
                        if (copy.HasProperty("baseColorTexture")) copy.SetTexture("baseColorTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                        EditorUtility.SetDirty(copy);
                        swaps[m] = copy;
                    }
                    ms[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = ms;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneryKit] Gameplay prefabs now use {swaps.Count} compressed materials.");
    }

    static Material BuildMaterial(Spec s, GameObject model)
    {
        // A copy of the model's own glTF material (same shader and settings, so it looks exactly
        // like the original), pointing at the compressed copy of its texture.
        string path = $"{MatFolder}/{s.name}.mat";
        var src = FirstMaterial(model);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(src);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = src.shader;
            m.CopyPropertiesFromMaterial(src);
        }
        foreach (var p in m.GetTexturePropertyNames())
            if (m.GetTexture(p) != null && AssetDatabase.GetAssetPath(m.GetTexture(p)).EndsWith(".glb")) m.SetTexture(p, null);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath(s));
        if (m.HasProperty("baseColorTexture")) m.SetTexture("baseColorTexture", tex);
        else m.SetTexture("_BaseMap", tex);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }
}
