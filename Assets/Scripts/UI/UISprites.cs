using UnityEngine;

/// <summary>Simple UI shapes drawn in code at startup (rounded box, circle, ring, triangle), so the HUD needs no image files.</summary>
public static class UISprites
{
    static Sprite rounded, circle, ring, triangle, vignette;

    /// <summary>Clear in the middle, solid at the edges (for the "you got hit" red flash).</summary>
    public static Sprite Vignette
    {
        get
        {
            if (vignette != null) return vignette;
            const int size = 128;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return vignette;
        }
    }

    public static Sprite RoundedRect { get { if (rounded == null) rounded = MakeRounded(64, 18); return rounded; } }
    public static Sprite Circle { get { if (circle == null) circle = MakeCircle(128, 0f); return circle; } }
    public static Sprite Ring { get { if (ring == null) ring = MakeCircle(128, 18f); return ring; } }
    static Sprite gaugeRing;
    /// <summary>A thick ring (inner radius about two thirds of the outer) for gauge fills.</summary>
    public static Sprite GaugeRing { get { if (gaugeRing == null) gaugeRing = MakeCircle(256, 42f); return gaugeRing; } }
    public static Sprite Triangle { get { if (triangle == null) triangle = MakeTriangle(128); return triangle; } }

    static Sprite glow, rays, turboArc, hubRing, turboArcGlow;

    /// <summary>A wide, soft band of orange light that spills past the Turbo channel's edges (same arc layout as TurboArc).</summary>
    public static Sprite TurboArcGlow
    {
        get
        {
            if (turboArcGlow != null) return turboArcGlow;
            const int size = 128;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float g = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.14f, 2f));     // soft bell round the channel's middle
                px[y * size + x] = new Color(1f, 0.62f, 0.2f, g);
            }
            tex.SetPixels32(px);
            tex.Apply();
            turboArcGlow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return turboArcGlow;
        }
    }

    /// <summary>
    /// The Turbo gauge's glowing channel (as in the pause concept): a band from 64% to 98% of the
    /// radius, bright yellow at its inner edge through orange to deep red-orange at the outer edge,
    /// soft at both edges, and brighter along the arc towards its leading end. The arc runs clockwise
    /// from the bottom of the texture, matching an Image radial fill with origin Bottom.
    /// </summary>
    public static Sprite TurboArc
    {
        get
        {
            if (turboArc != null) return turboArc;
            const int size = 256;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            Color hot = new Color(1f, 0.97f, 0.78f), yellow = new Color(1f, 0.8f, 0.3f), orange = new Color(1f, 0.54f, 0.12f), deep = new Color(0.82f, 0.26f, 0.05f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float u = (r - 0.64f) / 0.34f;
                if (u < 0f || u > 1f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                float edge = Mathf.SmoothStep(0f, 1f, u / 0.1f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.22f);
                // Hot white-yellow at the inner edge, orange through the middle, deep red-orange at the rim.
                Color c = u < 0.25f ? Color.Lerp(hot, yellow, u / 0.25f)
                        : u < 0.6f ? Color.Lerp(yellow, orange, (u - 0.25f) / 0.35f)
                        : Color.Lerp(orange, deep, (u - 0.6f) / 0.4f);
                float ang = Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg;        // clockwise from the bottom
                if (ang < 0f) ang += 360f;
                float along = Mathf.Clamp01(ang / 270f);
                c *= Mathf.Lerp(0.85f, 1.05f, along);
                // Light, not paint: see-through enough for the dial's tick marks, strongest at the hot inner edge.
                float a = edge * Mathf.Lerp(0.66f, 0.84f, along) * Mathf.Lerp(1.08f, 0.82f, u);
                px[y * size + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            turboArc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return turboArc;
        }
    }

    /// <summary>A thin soft ring that fades along its length like a comet's tail (spun round the Turbo hub).</summary>
    public static Sprite HubRing
    {
        get
        {
            if (hubRing != null) return hubRing;
            const int size = 128;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float band = 1f - Mathf.Abs(r - 0.86f) / 0.1f;
                if (band <= 0f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                float tail = Mathf.Pow(ang / 360f, 1.6f);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, band) * Mathf.Lerp(0.08f, 1f, tail));
            }
            tex.SetPixels32(px);
            tex.Apply();
            hubRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return hubRing;
        }
    }

    /// <summary>Soft radial glow, bright in the middle fading to nothing.</summary>
    public static Sprite Glow
    {
        get
        {
            if (glow != null) return glow;
            const int size = 128;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 2.2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return glow;
        }
    }

    /// <summary>Cartoon sunburst: 12 soft rays fading out from the centre (spin it behind a trophy).</summary>
    public static Sprite Rays
    {
        get
        {
            if (rays != null) return rays;
            const int size = 256;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float ray = Mathf.Cos(ang * 12f);                       // 12 rays
                float edge = Mathf.Clamp01((ray - 0.2f) * 2.5f * (0.4f + d * 2f)); // crisp-ish, wider at the rim
                float fade = Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 4f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(edge * fade * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            rays = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return rays;
        }
    }

    static Sprite coinGold, coinRed, chevron;

    /// <summary>
    /// A glossy cartoon coin for the chest pointers (gold for a chest, red with a gold rim for the
    /// Jackpot chest): dark outline, bevelled rim lit from the top-left, an engraved inner ring and
    /// a soft highlight. Drawn in full colour, so use it with Image.color = white.
    /// </summary>
    public static Sprite ChestCoin(bool jackpot)
    {
        if (jackpot && coinRed != null) return coinRed;
        if (!jackpot && coinGold != null) return coinGold;
        const int size = 128;
        var tex = NewTex(size);
        var px = new Color32[size * size];
        Color face = jackpot ? new Color(0.93f, 0.2f, 0.16f) : new Color(1f, 0.76f, 0.2f);
        Color faceLight = jackpot ? new Color(1f, 0.45f, 0.36f) : new Color(1f, 0.92f, 0.5f);
        Color rimCol = jackpot ? new Color(1f, 0.74f, 0.22f) : new Color(0.86f, 0.52f, 0.1f);
        Color rimDark = jackpot ? new Color(0.7f, 0.36f, 0.06f) : new Color(0.55f, 0.3f, 0.05f);
        Color outline = jackpot ? new Color(0.24f, 0.04f, 0.03f) : new Color(0.3f, 0.16f, 0.03f);
        Vector2 light = new Vector2(-0.6f, 0.8f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float aa = 2f / size;                                   // one pixel in normalised units
            float alpha = Mathf.Clamp01((0.98f - r) / aa);
            float lit = r > 0.001f ? Vector2.Dot(new Vector2(dx, dy) / r, light) : 0f;
            Color c;
            if (r > 0.9f) c = outline;
            else if (r > 0.74f) c = Color.Lerp(rimDark, rimCol, 0.5f + 0.5f * lit);           // bevelled rim
            else
            {
                c = Color.Lerp(face, faceLight, Mathf.Clamp01(0.5f + dy * 0.6f));              // lighter at the top
                c = Color.Lerp(c, rimDark, Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.02f, 2f)) * 0.7f); // engraved ring
                c = Color.Lerp(c, Color.Lerp(face, Color.black, 0.25f), Mathf.Clamp01((r - 0.6f) / 0.14f) * Mathf.Clamp01(-lit) * 0.5f);
            }
            // Blend between bands so the rings aren't jagged.
            if (r > 0.9f - aa && r < 0.9f + aa) c = Color.Lerp(Color.Lerp(rimDark, rimCol, 0.5f + 0.5f * lit), outline, (r - (0.9f - aa)) / (2f * aa));
            // Soft highlight, top-left.
            float hx = (dx + 0.32f) / 0.34f, hy = (dy - 0.38f) / 0.17f;
            float hl = Mathf.Clamp01(1f - (hx * hx + hy * hy)) * 0.65f;
            c = Color.Lerp(c, Color.white, hl);
            c.a = alpha;
            px[y * size + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        if (jackpot) coinRed = s; else coinGold = s;
        return s;
    }

    /// <summary>A chunky rounded chevron pointing up, white with a darker outline (tint it).</summary>
    public static Sprite Chevron
    {
        get
        {
            if (chevron != null) return chevron;
            const int size = 96;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            // Distance to a "^" polyline: two thick segments meeting at the tip.
            Vector2 tip = new Vector2(0.5f, 0.78f), l = new Vector2(0.14f, 0.3f), rr = new Vector2(0.86f, 0.3f);
            const float half = 0.13f, edge = 0.035f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float d = Mathf.Min(SegDist(p, l, tip), SegDist(p, rr, tip));
                float a = Mathf.Clamp01((half - d) * size);
                float body = Mathf.Clamp01((half - edge - d) * size);
                float shade = Mathf.Lerp(0.32f, 1f, body);
                byte s = (byte)(shade * 255);
                px[y * size + x] = new Color32(s, s, s, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            chevron = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return chevron;
        }
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static Texture2D NewTex(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        return t;
    }

    static Sprite MakeRounded(int size, int radius)
    {
        var tex = NewTex(size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            byte a = (byte)(Mathf.Clamp01(radius - d) * 255);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static Sprite MakeCircle(int size, float ringThickness)
    {
        var tex = NewTex(size);
        var px = new Color32[size * size];
        float r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
            float a = Mathf.Clamp01(r - d);
            if (ringThickness > 0f) a *= Mathf.Clamp01(d - (r - ringThickness));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Triangle pointing up.</summary>
    static Sprite MakeTriangle(int size)
    {
        var tex = NewTex(size);
        var px = new Color32[size * size];
        Vector2 a = new Vector2(size * 0.5f, size * 0.95f), b = new Vector2(size * 0.08f, size * 0.08f), c = new Vector2(size * 0.92f, size * 0.08f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(EdgeDist(p, a, b), Mathf.Min(EdgeDist(p, b, c), EdgeDist(p, c, a)));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Signed distance to the edge a→b (positive on the inside for counter-clockwise a,b,c).
    static float EdgeDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a;
        Vector2 n = new Vector2(e.y, -e.x).normalized;
        return -Vector2.Dot(p - a, n);
    }
}
