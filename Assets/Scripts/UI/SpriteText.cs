using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A line of Lefika's painted letters and digits (Resources/MenuArt/Glyphs, cut from Docs/NPC_Naming by
/// MenuArtBuilder). The glyphs are light grey with their grain kept, so the whole line takes any tint:
/// car colours for names, pale tints for the plate numbers. All glyphs share one height; widths follow
/// each glyph's own shape. The line is centred on this RectTransform.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SpriteText : MonoBehaviour
{
    readonly List<Image> glyphs = new List<Image>();
    string text;
    float height = 20f, spacing = 0.04f;
    Color color = Color.white;

    public static SpriteText Create(Transform parent, string name, float height, Color color, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        var t = go.AddComponent<SpriteText>();
        t.height = height;
        t.color = color;
        return t;
    }

    public Color Color
    {
        get => color;
        set
        {
            if (color == value) return;
            color = value;
            foreach (var g in glyphs) g.color = value;
        }
    }

    public void SetText(string s)
    {
        s = s.ToUpperInvariant();
        if (s == text) return;
        text = s;
        float space = height * 0.38f, gap = height * spacing;
        // Measure, then lay out centred.
        float total = 0f;
        var sprites = new List<Sprite>(s.Length);
        foreach (char c in s)
        {
            var sp = c == ' ' ? null : UIKit.Art("Glyphs/" + c);
            sprites.Add(sp);
            total += sp != null ? height * sp.rect.width / sp.rect.height + gap : space;
        }
        total -= gap;
        while (glyphs.Count < s.Length)
        {
            var img = Hud.NewImage("Glyph", transform, null, color);
            img.raycastTarget = false;
            glyphs.Add(img);
        }
        float x = -total * 0.5f;
        for (int i = 0; i < glyphs.Count; i++)
        {
            var img = glyphs[i];
            var sp = i < sprites.Count ? sprites[i] : null;
            bool on = sp != null;
            img.gameObject.SetActive(on);
            if (i >= s.Length) continue;
            if (!on) { x += space; continue; }
            float w = height * sp.rect.width / sp.rect.height;
            img.sprite = sp;
            img.color = color;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, height);
            r.anchoredPosition = new Vector2(x + w * 0.5f, 0f);
            x += w + gap;
        }
        ((RectTransform)transform).sizeDelta = new Vector2(total, height);
    }
}
