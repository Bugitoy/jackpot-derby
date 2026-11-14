using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UIWeight { Medium, SemiBold, Bold, ExtraBold }

/// <summary>
/// The painted menu art (Resources/MenuArt, cut from Lefika's Docs/Menu_Assets by MenuArtBuilder) and
/// the Poppins fonts (Resources/UIFonts), plus small builders for the pieces every screen uses:
/// art images, labels, painted buttons and sliders.
/// </summary>
public static class UIKit
{
    public static readonly Color Ink = new Color(0.14f, 0.09f, 0.04f);
    public static readonly Color Cream = new Color(0.97f, 0.93f, 0.83f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.18f);

    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static readonly Dictionary<UIWeight, TMP_FontAsset> fonts = new Dictionary<UIWeight, TMP_FontAsset>();
    static readonly Dictionary<TMP_FontAsset, Material> outlined = new Dictionary<TMP_FontAsset, Material>();

    public static Sprite Art(string name)
    {
        if (!sprites.TryGetValue(name, out var s) || s == null) sprites[name] = s = Resources.Load<Sprite>("MenuArt/" + name);
        return s;
    }

    public static TMP_FontAsset Font(UIWeight w)
    {
        if (!fonts.TryGetValue(w, out var f) || f == null)
        {
            fonts[w] = f = Resources.Load<TMP_FontAsset>($"UIFonts/Poppins-{w} SDF");
            if (f != null && !outlined.ContainsKey(f))
            {
                outlined[f] = Resources.Load<Material>($"UIFonts/Poppins-{w} Outlined");
                // So rich text can switch weight mid-line: <font="Poppins-Bold SDF">.
                MaterialReferenceManager.AddFontAsset(f);
            }
        }
        return f;
    }

    /// <summary>The outlined + drop-shadow material for this font, if it is one of ours.</summary>
    public static Material Outlined(TMP_FontAsset f)
    {
        if (f == null) return null;
        outlined.TryGetValue(f, out var m);
        return m;
    }

    // ---------------- builders ----------------

    /// <summary>An art image. height sets the size; width follows the sprite's aspect unless given.</summary>
    public static Image ArtImage(Transform parent, string sprite, Vector2 anchor, Vector2 pos, float height, float width = -1f)
    {
        var s = Art(sprite);
        var img = Hud.NewImage(sprite, parent, s, Color.white);
        float w = width > 0f ? width : (s != null ? height * s.rect.width / s.rect.height : height);
        Hud.Anchor(img.rectTransform, anchor, pos, new Vector2(w, height));
        if (s != null && s.border != Vector4.zero) Slice(img, height);
        else img.type = Image.Type.Simple;
        return img;
    }

    /// <summary>9-slice an art image so its painted edges keep their proportions at this height.</summary>
    public static void Slice(Image img, float height)
    {
        if (img.sprite == null) return;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, img.sprite.rect.height / Mathf.Max(1f, height) * (img.sprite.pixelsPerUnit / 100f));
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, UIWeight weight = UIWeight.Bold, bool outline = false)
    {
        var t = Hud.NewText("Text", parent, text, size, color);
        var f = Font(weight);
        if (f != null) t.font = f;
        t.fontStyle = FontStyles.Normal;
        if (outline)
        {
            var m = Outlined(t.font);
            if (m != null) t.fontSharedMaterial = m;
        }
        return t;
    }

    /// <summary>Gold-to-orange title gradient, like the concept headings.</summary>
    public static void GoldGradient(TextMeshProUGUI t)
    {
        t.enableVertexGradient = true;
        t.color = Color.white;
        t.colorGradient = new VertexGradient(new Color(1f, 0.94f, 0.45f), new Color(1f, 0.94f, 0.45f), new Color(1f, 0.66f, 0.12f), new Color(1f, 0.66f, 0.12f));
    }

    /// <summary>
    /// A painted button: the art sprite, a label (optionally a small hint line under it), springy
    /// hover/press and sounds. Works while the game is paused.
    /// </summary>
    public static Button PaintedButton(Transform parent, string sprite, string label, Color ink, Vector2 anchor, Vector2 pos, Vector2 size,
                                       float fontSize, Action onClick, string hint = null, UIWeight weight = UIWeight.ExtraBold, bool outlineLabel = false)
    {
        var img = ArtImage(parent, sprite, anchor, pos, size.y, size.x);
        img.name = "Btn_" + label;
        img.raycastTarget = true;
        var t = Label(img.rectTransform, label, fontSize, ink, weight, outlineLabel);
        Hud.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, hint != null ? size.y * 0.12f : size.y * 0.03f), size);
        t.characterSpacing = 1f;
        if (hint != null)
        {
            var h = Label(img.rectTransform, hint, Mathf.Max(12f, fontSize * 0.3f), new Color(ink.r, ink.g, ink.b, 0.9f), UIWeight.SemiBold);
            Hud.Anchor(h.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, size.y * 0.22f), new Vector2(size.x, 18f));
        }
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        cb.fadeDuration = 0.06f;
        b.colors = cb;
        b.onClick.AddListener(() => onClick());
        img.gameObject.AddComponent<JuicyButton>();
        return b;
    }

    /// <summary>A painted slider: rusty track and the cream knob, with an outlined label on its left.</summary>
    public static Slider PaintedSlider(Transform parent, string label, Vector2 anchor, Vector2 pos, float labelWidth, float trackWidth,
                                       float value, Action<float> onChange, float height = 22f)
    {
        var row = Hud.NewRect("Slider_" + label, parent, pos, new Vector2(labelWidth + trackWidth, height + 10f));
        Hud.Anchor(row, anchor, pos, new Vector2(labelWidth + trackWidth, height + 10f));
        var t = Label(row, label, height * 0.82f, Cream, UIWeight.ExtraBold, true);
        t.alignment = TextAlignmentOptions.Left;
        Hud.Anchor(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(labelWidth * 0.5f, 0f), new Vector2(labelWidth, height + 6f));

        var track = ArtImage(row, "slider_track", new Vector2(1f, 0.5f), new Vector2(-trackWidth * 0.5f, 0f), height, trackWidth);
        track.raycastTarget = true;
        // Knob travels inside the track's rounded ends.
        var handleArea = Hud.NewRect("HandleArea", track.rectTransform, Vector2.zero, Vector2.zero);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(height * 0.55f, 0f);
        handleArea.offsetMax = new Vector2(-height * 0.55f, 0f);
        var knob = Hud.NewImage("Knob", handleArea, Art("slider_knob"), Color.white);
        knob.preserveAspect = true;
        knob.rectTransform.sizeDelta = new Vector2(height * 1.55f, height * 1.55f);
        knob.raycastTarget = true;

        var s = track.gameObject.AddComponent<Slider>();
        s.handleRect = knob.rectTransform;
        s.targetGraphic = knob;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;
        s.SetValueWithoutNotify(value);
        // Only the player moving the knob changes the setting. (Sliders also report their value on
        // their own, e.g. the editor's layout rebuild while a scene unloads, which once saved 0.)
        s.onValueChanged.AddListener(v =>
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || es.currentSelectedGameObject != s.gameObject) return;
            onChange(v);
        });
        return s;
    }
}
