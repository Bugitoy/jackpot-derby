using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Turns Lefika's menu art (Docs/Menu_Assets, painted UI pieces with transparent backgrounds) into
/// trimmed UI sprites in Assets/Resources/MenuArt, and makes the Poppins TextMeshPro fonts
/// (Assets/Resources/UIFonts) with outlined variants. Menu: Jackpot Derby/Build Menu Art.
/// Each sheet is cut into its separate pieces by finding the opaque islands in the alpha channel.
/// </summary>
public static class MenuArtBuilder
{
    const string SourceDir = "Docs/Menu_Assets";
    const string OutDir = "Assets/Resources/MenuArt";
    const string FontDir = "Assets/Resources/UIFonts";
    const string TtfDir = "Assets/UI/Fonts";

    /// <summary>One piece to cut out. border = 9-slice border as a share of the piece's height (0 = not sliced).</summary>
    struct Piece
    {
        public string name;
        /// <summary>9-slice border (left, bottom, right, top) as shares of the piece's height.</summary>
        public Vector4 border;
        public Piece(string n, float h = 0f, float v = -1f) { name = n; float vv = v < 0f ? h : v; border = new Vector4(h, vv, h, vv); }
        public Piece(string n, Vector4 lbrt) { name = n; border = lbrt; }
    }

    [MenuItem("Jackpot Derby/Build Menu Art")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(OutDir);
        var log = new System.Text.StringBuilder("[MenuArt] ");

        // Single pieces (everything opaque in the file is one piece).
        Single("r7bpb57d1drmr0d1v1vv23e43m", new Piece("logo"), log);
        Single("37hm2eq7f1rmw0d1v1tr8kvdtg", new Piece("board"), log);
        Single("dqe4h1w785rmt0d1v1t81b93f8", new Piece("card", 0.16f), log);
        Single("qvseaqy1jhrmr0d1v1wa8k9pjm", new Piece("steel_plate", 0.42f, 0.36f), log);
        Single("m8snqnz16hrmr0d1v1ta7rbxvc", new Piece("hp_frame"), log);
        // Warning sign on the left stays square; the paper stretches.
        Single("6e328jek71rmw0d1v1va2t5za4", new Piece("storm_banner", new Vector4(0.9f, 0.12f, 0.3f, 0.12f)), log);
        Single("a4ce27sfm9rmt0d1v1t9t8vfcg", new Piece("turbo_dial"), log);

        // Sheets with several pieces, named in reading order (rows top to bottom, then left to right).
        Sheet("d71az8svqhrmy0d1v1ts2peqj8", log, ByRows, new Piece("btn_yellow", 0.4f), new Piece("btn_teal", 0.4f), new Piece("btn_slate", 0.4f), new Piece("btn_cream", 0.4f));
        Sheet("pe5j7vs2m5rmr0d1v1vvzpz280", log, ByColumnsThenRows, new Piece("btn_yellow_big", 0.4f), new Piece("btn_teal_big", 0.4f), new Piece("plaque"));
        Sheet("txddjex2wxrmr0d1v1ttx1y9tw", log, ByColumns, new Piece("slider_track_cap", 0.5f), new Piece("slider_knob"), new Piece("slider_track", 0.5f));
        Sheet("xxs6z0jh75rmw0d1v1v9gywhn4", log, ByColumns, new Piece("storm_banner_plate"), new Piece("timer_screen", 0.3f));

        // The odometer: the frame, and the padlock body cut free so it can swing and light up.
        Odometer(log);

        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(OutDir, "*.png")) ConfigureSprite(f.Replace('\\', '/'));
        BuildFonts(log);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    // ---------------- cutting ----------------

    static Texture2D Load(string file)
    {
        var path = Path.Combine(SourceDir, file + "_preview_0-ezremove.png");
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        return tex;
    }

    /// <summary>Bounding boxes of the opaque islands, nearby islands merged (ivy leaves, rust specks).</summary>
    static List<RectInt> Islands(Texture2D tex, int minPixels = 400, int mergeGap = 28)
    {
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        var label = new int[w * h];
        var boxes = new List<RectInt>();
        var counts = new List<int>();
        var stack = new Stack<int>();
        for (int i = 0; i < px.Length; i++)
        {
            if (label[i] != 0 || px[i].a < 40) continue;
            int id = boxes.Count + 1, n = 0;
            int minX = w, minY = h, maxX = 0, maxY = 0;
            label[i] = id; stack.Push(i);
            while (stack.Count > 0)
            {
                int p = stack.Pop(); n++;
                int x = p % w, y = p / w;
                if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int q = ny * w + nx;
                    if (label[q] != 0 || px[q].a < 40) continue;
                    label[q] = id; stack.Push(q);
                }
            }
            boxes.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
            counts.Add(n);
        }
        var keep = new List<RectInt>();
        for (int i = 0; i < boxes.Count; i++) if (counts[i] >= minPixels) keep.Add(boxes[i]);
        // Merge islands closer than mergeGap.
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int a = 0; a < keep.Count && !merged; a++)
            for (int b = a + 1; b < keep.Count && !merged; b++)
            {
                var A = keep[a]; var B = keep[b];
                bool near = A.xMin - mergeGap <= B.xMax && B.xMin - mergeGap <= A.xMax && A.yMin - mergeGap <= B.yMax && B.yMin - mergeGap <= A.yMax;
                if (!near) continue;
                int x0 = Mathf.Min(A.xMin, B.xMin), y0 = Mathf.Min(A.yMin, B.yMin), x1 = Mathf.Max(A.xMax, B.xMax), y1 = Mathf.Max(A.yMax, B.yMax);
                keep[a] = new RectInt(x0, y0, x1 - x0, y1 - y0);
                keep.RemoveAt(b);
                merged = true;
            }
        }
        // Tiny leftovers (lone specks) are dropped.
        keep.RemoveAll(r => r.width * r.height < 2500);
        return keep;
    }

    static void ByRows(List<RectInt> r) => r.Sort((a, b) => b.yMax.CompareTo(a.yMax));          // texture y is up: top first
    static void ByColumns(List<RectInt> r) => r.Sort((a, b) => a.xMin.CompareTo(b.xMin));
    /// <summary>Left column top to bottom, then the next column.</summary>
    static void ByColumnsThenRows(List<RectInt> r) => r.Sort((a, b) =>
        Mathf.Abs(a.xMin - b.xMin) > 150 ? a.xMin.CompareTo(b.xMin) : b.yMax.CompareTo(a.yMax));

    static void Single(string file, Piece piece, System.Text.StringBuilder log)
    {
        var tex = Load(file);
        var isl = Islands(tex);
        if (isl.Count == 0) { log.Append($"{piece.name}: nothing opaque! "); return; }
        var all = isl[0];
        foreach (var r in isl) all = Union(all, r);
        Save(tex, all, piece, log, null);
        Object.DestroyImmediate(tex);
    }

    static void Sheet(string file, System.Text.StringBuilder log, System.Action<List<RectInt>> order, params Piece[] pieces)
    {
        var tex = Load(file);
        var isl = Islands(tex);
        order(isl);
        if (isl.Count != pieces.Length) log.Append($"[{file}: found {isl.Count} pieces, expected {pieces.Length}] ");
        for (int i = 0; i < pieces.Length && i < isl.Count; i++) Save(tex, isl[i], pieces[i], log, null);
        Object.DestroyImmediate(tex);
    }

    static void Odometer(System.Text.StringBuilder log)
    {
        var tex = Load("01m2x82fqnrmt0d1v1sv0bc6x8");
        int h = tex.height;
        // Padlock body in image coordinates (y down): x 1250-1425, y 712-885. Texture y is up.
        var lockRect = new RectInt(1250, h - 885, 175, 885 - 712);
        var isl = Islands(tex);
        var all = isl[0];
        foreach (var r in isl) all = Union(all, r);
        Save(tex, lockRect, new Piece("odometer_padlock"), log, null);
        Save(tex, all, new Piece("odometer_frame"), log, lockRect);
        Object.DestroyImmediate(tex);
    }

    static RectInt Union(RectInt a, RectInt b)
    {
        int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin), x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Crop (with a little padding) and write the PNG. erase = a region to make transparent.</summary>
    static void Save(Texture2D src, RectInt r, Piece piece, System.Text.StringBuilder log, RectInt? erase)
    {
        const int pad = 4;
        int x0 = Mathf.Max(0, r.xMin - pad), y0 = Mathf.Max(0, r.yMin - pad);
        int x1 = Mathf.Min(src.width, r.xMax + pad), y1 = Mathf.Min(src.height, r.yMax + pad);
        int w = x1 - x0, hgt = y1 - y0;
        var outTex = new Texture2D(w, hgt, TextureFormat.RGBA32, false);
        var px = src.GetPixels(x0, y0, w, hgt);
        if (erase.HasValue)
        {
            var e = erase.Value;
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
                if (e.Contains(new Vector2Int(x + x0, y + y0))) px[y * w + x] = Color.clear;
        }
        outTex.SetPixels(px);
        outTex.Apply();
        File.WriteAllBytes($"{OutDir}/{piece.name}.png", outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        // Image coordinates (y down) of the crop, so layouts can map painted features onto the sprite.
        log.Append($"{piece.name} [{x0},{src.height - y1} {w}x{hgt}] ");
        borders[piece.name] = piece.border * hgt;
    }

    static readonly Dictionary<string, Vector4> borders = new Dictionary<string, Vector4>();

    static void ConfigureSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.spritePixelsPerUnit = 100f;
        string name = Path.GetFileNameWithoutExtension(path);
        if (borders.TryGetValue(name, out var b)) imp.spriteBorder = b;
        imp.SaveAndReimport();
    }

    // ---------------- fonts ----------------

    static void BuildFonts(System.Text.StringBuilder log)
    {
        Directory.CreateDirectory(FontDir);
        var outlineSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/HudText_Outlined.mat");
        foreach (var weight in new[] { "Medium", "SemiBold", "Bold", "ExtraBold" })
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{TtfDir}/Poppins-{weight}.ttf");
            if (ttf == null) { log.Append($"[missing Poppins-{weight}] "); continue; }
            string assetPath = $"{FontDir}/Poppins-{weight} SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fa == null)
            {
                fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = $"Poppins-{weight} SDF";
                AssetDatabase.CreateAsset(fa, assetPath);
                // The atlas texture and material live inside the font asset.
                fa.atlasTextures[0].name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            // Bake every character the game prints, so builds never have to render glyphs on the fly.
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~•×–—’…·", out _);
            EditorUtility.SetDirty(fa);

            // Outlined + drop-shadow preset on this font's atlas, with the HUD's outline settings.
            string matPath = $"{FontDir}/Poppins-{weight} Outlined.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(fa.material); AssetDatabase.CreateAsset(m, matPath); }
            if (outlineSource != null) m.CopyPropertiesFromMaterial(outlineSource);
            m.shader = fa.material.shader;
            m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, fa.atlasWidth);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, fa.atlasHeight);
            m.SetFloat(ShaderUtilities.ID_GradientScale, fa.atlasPadding + 1);
            m.EnableKeyword("OUTLINE_ON");
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.12f, 0.07f, 0.03f, 1f));
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            EditorUtility.SetDirty(m);
            log.Append($"font {weight} ok ({fa.characterTable.Count} chars) ");
        }
    }
}
