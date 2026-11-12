using UnityEngine;

/// <summary>Simple UI shapes drawn in code at startup (rounded box, circle, ring, triangle), so the HUD needs no image files.</summary>
public static class UISprites
{
    static Sprite rounded, circle, ring, triangle;

    public static Sprite RoundedRect { get { if (rounded == null) rounded = MakeRounded(64, 18); return rounded; } }
    public static Sprite Circle { get { if (circle == null) circle = MakeCircle(128, 0f); return circle; } }
    public static Sprite Ring { get { if (ring == null) ring = MakeCircle(128, 18f); return ring; } }
    public static Sprite Triangle { get { if (triangle == null) triangle = MakeTriangle(128); return triangle; } }

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
