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
        Single("logo_jackpot_derby.png", new Piece("logo"), log);
        Single("pause_board_ivy.png", new Piece("board"), log);
        Single("metal_card.png", new Piece("card", 0.16f), log);
        Single("steel_plate.png", new Piece("steel_plate", 0.42f, 0.36f), log);
        Single("hp_frame.png", new Piece("hp_frame"), log);
        // Warning sign on the left stays square; the paper stretches.
        Single("storm_warning_banner.png", new Piece("storm_banner", new Vector4(0.9f, 0.12f, 0.3f, 0.12f)), log);
        Single("turbo_dial.png", new Piece("turbo_dial"), log);

        // Sheets with several pieces, named in reading order (rows top to bottom, then left to right).
        Sheet("buttons_pause_sheet.png", log, ByRows, new Piece("btn_yellow", 0.4f), new Piece("btn_teal", 0.4f), new Piece("btn_slate", 0.4f), new Piece("btn_cream", 0.4f));
        Sheet("buttons_title_sheet.png", log, ByColumnsThenRows, new Piece("btn_yellow_big", 0.4f), new Piece("btn_teal_big", 0.4f), new Piece("plaque"));
        Sheet("slider_parts_sheet.png", log, ByColumns, new Piece("slider_track_cap", 0.5f), new Piece("slider_knob"), new Piece("slider_track", 0.5f));
        Sheet("storm_banner_and_timer_sheet.png", log, ByColumns, new Piece("storm_banner_plate"), new Piece("timer_screen", 0.3f));

        // The odometer: the frame, and the padlock body cut free so it can swing and light up.
        Odometer(log);
        BuildNpcArt(log);

        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(OutDir, "*.png", SearchOption.AllDirectories)) ConfigureSprite(f.Replace('\\', '/'));
        BuildFonts(log);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    // ---------------- cutting ----------------

    static Texture2D Load(string file)
    {
        var path = file.Contains("/") ? file : Path.Combine(SourceDir, file);
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
        var tex = Load("odometer_frame.png");
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

    // ---------------- NPC plates, name letters and health bar (Docs/NPC_Naming) ----------------

    const string NpcDir = "Docs/NPC_Naming";
    const string GlyphDir = OutDir + "/Glyphs";

    /// <summary>
    /// The art over each car: the four oval number plates, the rusty health bar frame, and textured
    /// letters/digits converted to light grey (grain and shading kept) so they can be tinted to any car
    /// colour. The orange letter set is the only complete A-Z; the digit art has no 0, so 0 is the
    /// letter O narrowed to digit width.
    /// </summary>
    static void BuildNpcArt(System.Text.StringBuilder log)
    {
        Directory.CreateDirectory(GlyphDir);

        // Plates: purple, orange, red, green from top to bottom (each with its painted underline).
        var plates = Load(NpcDir + "/npc_number_plates_sheet.png");
        var pl = Islands(plates, 200, 8);
        ByRows(pl);
        string[] plateNames = { "npc_plate_purple", "npc_plate_orange", "npc_plate_red", "npc_plate_green" };
        if (pl.Count != 4) log.Append($"[plates: found {pl.Count}, expected 4] ");
        for (int i = 0; i < 4 && i < pl.Count; i++) Save(plates, pl[i], new Piece(plateNames[i]), log, null);
        Object.DestroyImmediate(plates);

        // Health bar frame: cut out of the JPG with rounded corners (its checkerboard was painted in).
        var bar = Load(NpcDir + "/npc_health_bar_frame.jpg");
        var barRect = new RectInt(229, bar.height - 1021, 2302, 1021 - 483);   // just inside the painted edge
        SaveRounded(bar, barRect, 34f, "npc_bar_frame", log);
        Object.DestroyImmediate(bar);

        // Letters A-Z from the orange set (rows 4-6 of the sheet).
        var letters = Load(NpcDir + "/npc_letters_sheet.png");
        var lg = GlyphsInBand(letters, 390, 745);
        if (lg.Count != 26) log.Append($"[letters: found {lg.Count}, expected 26] ");
        for (int i = 0; i < 26 && i < lg.Count; i++) SaveGlyph(letters, lg[i], ((char)('A' + i)).ToString(), 1f);

        // Digits 1-9 from the purple set (rows 1-2: 1 2 3 4 5 6 / 7 7 8 9).
        var digits = Load(NpcDir + "/npc_digits_sheet.png");
        var dg = GlyphsInBand(digits, 20, 430);
        string[] digitNames = { "1", "2", "3", "4", "5", "6", "7", null, "8", "9" };
        if (dg.Count != digitNames.Length) log.Append($"[digits: found {dg.Count}, expected {digitNames.Length}] ");
        float digitAspect = 0f; int nDigits = 0;
        for (int i = 0; i < digitNames.Length && i < dg.Count; i++)
        {
            if (digitNames[i] == null) continue;
            SaveGlyph(digits, dg[i], digitNames[i], 1f);
            if (digitNames[i] != "1") { digitAspect += (float)dg[i].width / dg[i].height; nDigits++; }
        }
        // 0 = the letter O squeezed to a digit's proportions (the art has no zero). Glyphs are drawn
        // at a common height, so matching the width-to-height ratio is what counts.
        if (lg.Count >= 15 && nDigits > 0)
        {
            var o = lg[14];
            float target = digitAspect / nDigits * 0.92f;
            SaveGlyph(letters, o, "0", Mathf.Clamp(target / ((float)o.width / o.height), 0.4f, 1f));
        }
        Object.DestroyImmediate(letters);
        Object.DestroyImmediate(digits);
        log.Append($"npc art: {pl.Count} plates, bar frame, {lg.Count} letters, {dg.Count} digit pieces ");
    }

    /// <summary>Glyph islands whose centre lies in a band of image rows (y down), in reading order.</summary>
    static List<RectInt> GlyphsInBand(Texture2D tex, int yTop, int yBottom)
    {
        var all = Islands(tex, 300, 2);
        var band = new List<RectInt>();
        foreach (var r in all)
        {
            float cy = tex.height - r.center.y;            // to image rows, y down
            if (cy >= yTop && cy <= yBottom) band.Add(r);
        }
        // Rows: cluster by centre height, then left to right.
        band.Sort((a, b) => b.center.y.CompareTo(a.center.y));
        var rows = new List<List<RectInt>>();
        foreach (var r in band)
        {
            if (rows.Count == 0 || Mathf.Abs(rows[rows.Count - 1][0].center.y - r.center.y) > 70f) rows.Add(new List<RectInt>());
            rows[rows.Count - 1].Add(r);
        }
        var ordered = new List<RectInt>();
        foreach (var row in rows) { row.Sort((a, b) => a.xMin.CompareTo(b.xMin)); ordered.AddRange(row); }
        return ordered;
    }

    /// <summary>Save a glyph as light grey (luminance normalised so its face is ~white), optionally narrowed.</summary>
    static void SaveGlyph(Texture2D src, RectInt r, string name, float widthScale)
    {
        const int pad = 3;
        int x0 = Mathf.Max(0, r.xMin - pad), y0 = Mathf.Max(0, r.yMin - pad);
        int x1 = Mathf.Min(src.width, r.xMax + pad), y1 = Mathf.Min(src.height, r.yMax + pad);
        int w = x1 - x0, h = y1 - y0;
        var px = src.GetPixels(x0, y0, w, h);
        // Face brightness = a high percentile of the opaque pixels' luminance.
        var lums = new List<float>();
        foreach (var c in px) if (c.a > 0.6f) lums.Add(0.3f * c.r + 0.59f * c.g + 0.11f * c.b);
        lums.Sort();
        float face = lums.Count > 0 ? Mathf.Max(0.05f, lums[(int)(lums.Count * 0.9f)]) : 1f;
        int ow = Mathf.Max(4, Mathf.RoundToInt(w * widthScale));
        var outTex = new Texture2D(ow, h, TextureFormat.RGBA32, false);
        var outPx = new Color[ow * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < ow; x++)
        {
            // Bilinear resample across X when narrowing.
            float sx = (x + 0.5f) / widthScale - 0.5f;
            int ix = Mathf.Clamp(Mathf.FloorToInt(sx), 0, w - 1), ix2 = Mathf.Min(w - 1, ix + 1);
            float f = Mathf.Clamp01(sx - ix);
            Color c = Color.Lerp(px[y * w + ix], px[y * w + ix2], f);
            float l = Mathf.Clamp01((0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / face);
            outPx[y * ow + x] = new Color(l, l, l, c.a);
        }
        outTex.SetPixels(outPx);
        outTex.Apply();
        File.WriteAllBytes($"{GlyphDir}/{name}.png", outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
    }

    /// <summary>Crop a rectangle and round its corners off into transparency.</summary>
    static void SaveRounded(Texture2D src, RectInt r, float radius, string name, System.Text.StringBuilder log)
    {
        var px = src.GetPixels(r.xMin, r.yMin, r.width, r.height);
        int w = r.width, h = r.height;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, w - radius), cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            var c = px[y * w + x];
            c.a = Mathf.Clamp01(radius - d + 0.5f);
            px[y * w + x] = c;
        }
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.SetPixels(px);
        outTex.Apply();
        File.WriteAllBytes($"{OutDir}/{name}.png", outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        log.Append($"{name} [{r.xMin},{src.height - r.yMax} {w}x{h}] ");
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

        // <b> in Medium text (the How to Play cards) draws real Poppins Bold, not a faux bold.
        // Saved in the font asset, so it works without any runtime font lookup.
        var medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/Poppins-Medium SDF.asset");
        var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/Poppins-Bold SDF.asset");
        if (medium != null && bold != null)
        {
            medium.fontWeightTable[7].regularTypeface = bold;   // edits the asset's own table in place
            EditorUtility.SetDirty(medium);
        }
    }
}
