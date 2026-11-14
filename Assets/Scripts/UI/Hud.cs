using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's HUD, built in code at startup: odometer reels with padlock, hint line,
/// big popups (TOO HIGH, LUCKY!, SMALL BLASTER!...), blaster status, and chest markers/arrows.
/// </summary>
public partial class Hud : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] Color panelColor = new Color(0.08f, 0.16f, 0.18f, 0.92f);
    [SerializeField] Color reelColor = new Color(0.96f, 0.92f, 0.82f);
    [SerializeField] Color reelLockedColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] Color digitColor = new Color(0.12f, 0.12f, 0.14f);
    [SerializeField] Color gold = new Color(1f, 0.82f, 0.15f);
    [SerializeField] float popupHold = 1.3f;
    [Tooltip("Font material with outline + drop shadow (made by the SceneSetup editor script).")]
    [SerializeField] Material outlinedText;

    public static Hud Instance { get; private set; }

    const float DigitH = 84f;

    Canvas canvas;
    RectTransform canvasRect;
    Camera cam;
    CarIdentity player;
    Odometer odometer;
    CarLoadout loadout;

    RectTransform tensColumn, onesColumn;
    Image tensWindow, onesWindow, padBody, padShackle;
    RectTransform padShackleRect;
    TextMeshProUGUI hintLine, popupText, popupSub, cheatLine;
    RectTransform popupRect;
    float popupAge = 99f;
    float shownValue;
    int lastNumber;
    float lockPunch;

    class Marker { public RectTransform root; public Image bubble; public Image arrow; public TextMeshProUGUI label; }
    readonly List<Marker> markers = new List<Marker>();
    RectTransform markerLayer;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
        BuildCanvas();
        BuildCombatUI();
    }

    void Start()
    {
        TryBindPlayer();
        BindMatch();
    }

    void TryBindPlayer()
    {
        if (player != null) return;
        player = CarIdentity.Player;
        if (player == null) return;
        odometer = player.GetComponent<Odometer>();
        loadout = player.GetComponent<CarLoadout>();
        if (odometer != null)
        {
            odometer.HintRevealed += OnHint;
            odometer.LockChanged += OnLockChanged;
            shownValue = odometer.Number;
            lastNumber = odometer.Number;
        }
        if (loadout != null)
        {
            loadout.RewardMessage += (msg, col, sub) => ShowPopup(msg, col, sub);
        }
        BindPlayerCombat();
    }

    // ---------------- building ----------------

    void BuildCanvas()
    {
        var go = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)go.transform;
        hudGroup = go.AddComponent<CanvasGroup>();

        markerLayer = NewRect("Markers", canvasRect, Vector2.zero, Vector2.zero);
        Stretch(markerLayer);

        // Odometer panel, bottom centre.
        var panel = NewImage("OdometerPanel", canvasRect, UISprites.RoundedRect, panelColor);
        Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(250f, 128f));
        var title = NewText("Title", panel.rectTransform, "YOUR NUMBER", 15, new Color(0.75f, 0.9f, 0.88f));
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(-18f, -13f), new Vector2(200f, 20f));

        tensWindow = MakeReel(panel.rectTransform, new Vector2(-64f, -8f), out tensColumn);
        onesWindow = MakeReel(panel.rectTransform, new Vector2(4f, -8f), out onesColumn);

        // Padlock: ring shackle behind a rounded body.
        var pad = NewRect("Padlock", panel.rectTransform, new Vector2(84f, -6f), new Vector2(44f, 60f));
        padShackle = NewImage("Shackle", pad, UISprites.Ring, gold);
        padShackleRect = padShackle.rectTransform;
        Anchor(padShackleRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(30f, 34f));
        padBody = NewImage("Body", pad, UISprites.RoundedRect, gold);
        Anchor(padBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(40f, 32f));
        var keyhole = NewImage("Keyhole", padBody.rectTransform, UISprites.Circle, new Color(0.1f, 0.1f, 0.1f, 0.8f));
        Anchor(keyhole.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));

        hintLine = NewText("HintLine", canvasRect, "", 26, Color.white);
        Anchor(hintLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(500f, 34f));
        hintLine.fontStyle = FontStyles.Bold;
        Outline(hintLine);

        // Big popup.
        popupText = NewText("Popup", canvasRect, "", 78, Color.white);
        popupText.fontStyle = FontStyles.Bold;
        Outline(popupText);
        popupRect = popupText.rectTransform;
        Anchor(popupRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1000f, 100f));
        popupSub = NewText("PopupSub", popupRect, "", 26, Color.white);
        Anchor(popupSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -18f), new Vector2(900f, 34f));
        Outline(popupSub);
        popupText.gameObject.SetActive(false);

        cheatLine = NewText("CheatHelp", canvasRect, "", 13, new Color(1f, 1f, 1f, 0.7f));
        cheatLine.alignment = TextAlignmentOptions.TopLeft;
        Anchor(cheatLine.rectTransform, new Vector2(0f, 1f), new Vector2(200f, -60f), new Vector2(380f, 60f));
    }

    Image MakeReel(RectTransform parent, Vector2 pos, out RectTransform column)
    {
        var window = NewImage("Reel", parent, UISprites.RoundedRect, reelColor);
        Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(62f, DigitH));
        window.gameObject.AddComponent<RectMask2D>();
        column = NewRect("Column", window.rectTransform, Vector2.zero, new Vector2(62f, DigitH * 11));
        column.anchorMin = column.anchorMax = new Vector2(0.5f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        for (int i = 0; i <= 10; i++)
        {
            var t = NewText("D" + i, column, (i % 10).ToString(), 70, digitColor);
            t.fontStyle = FontStyles.Bold;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -i * DigitH);
            r.sizeDelta = new Vector2(62f, DigitH);
        }
        // Shading strips top and bottom so it reads as a drum.
        var top = NewImage("ShadeTop", window.rectTransform, null, new Color(0, 0, 0, 0.18f));
        Anchor(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(62f, 14f));
        var bottom = NewImage("ShadeBottom", window.rectTransform, null, new Color(0, 0, 0, 0.18f));
        Anchor(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(62f, 14f));
        return window;
    }

    // ---------------- per frame ----------------

    void Update()
    {
        if (player == null) TryBindPlayer();
        if (odometer != null) UpdateReels();
        UpdatePopup();
        UpdateMarkers();
        UpdateCombatUI();
        if (hudGroup != null) hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, hudAlphaTarget, Time.unscaledDeltaTime * 2.5f);
        cheatLine.text = DebugCheats.Active
            ? "CHEATS  F2 chest numbers  F3 exact  F4 close  F5 spawn chest\nF6 arm enemies  F7 hurt me  F8 give me Jackpot  F9 kill nearest"
            : "";
    }

    void UpdateReels()
    {
        // Keep the roll continuous when the number wraps 99 <-> 1.
        int n = odometer.Number;
        if (lastNumber == 99 && n == 1) shownValue -= 99f;
        else if (lastNumber == 1 && n == 99) shownValue += 99f;
        lastNumber = n;

        float target = n + odometer.Progress;
        shownValue = Mathf.Lerp(shownValue, target, 1f - Mathf.Exp(-14f * Time.deltaTime));

        float v = Mathf.Repeat(shownValue, 100f);
        float ones = Mathf.Repeat(v, 10f);
        float tens = Mathf.Floor(v / 10f) + Mathf.Clamp01(ones - 9f);
        onesColumn.anchoredPosition = new Vector2(0f, ones * DigitH);
        tensColumn.anchoredPosition = new Vector2(0f, tens * DigitH);

        bool locked = odometer.IsLocked;
        lockPunch = Mathf.Max(0f, lockPunch - Time.deltaTime * 4f);
        Color w = locked ? reelLockedColor : reelColor;
        tensWindow.color = onesWindow.color = w;
        float s = 1f + 0.15f * Mathf.Sin(lockPunch * Mathf.PI);
        tensWindow.rectTransform.localScale = onesWindow.rectTransform.localScale = Vector3.one * s;

        padBody.color = locked ? gold : new Color(0.55f, 0.58f, 0.6f);
        padShackle.color = padBody.color;
        padShackleRect.anchoredPosition = locked ? new Vector2(0f, 10f) : new Vector2(7f, 18f);
        padShackleRect.localRotation = Quaternion.Euler(0f, 0f, locked ? 0f : -20f);
    }

    void OnLockChanged(bool locked)
    {
        if (locked) lockPunch = 1f;
        if (!locked) hintLine.text = "";
    }

    void OnHint(HintResult h)
    {
        hintLine.text = h.kind == HintKind.None ? "LOCKED - no chest yet" : (odometer.IsLocked ? "LOCKED: " : "") + h.Text;
        hintLine.color = h.Color;
        if (h.kind != HintKind.None) ShowPopup(h.Text, h.Color, "your number " + odometer.Number);
    }

    public void ShowPopup(string text, Color color, string sub)
    {
        popupText.text = text;
        popupText.color = color;
        popupSub.text = sub;
        popupAge = 0f;
        popupText.gameObject.SetActive(true);
    }

    void UpdatePopup()
    {
        if (!popupText.gameObject.activeSelf) return;
        popupAge += Time.deltaTime;
        float t = popupAge;
        float scale = t < 0.12f ? Mathf.Lerp(0.3f, 1.25f, t / 0.12f) : 1f + 0.25f * Mathf.Exp(-(t - 0.12f) * 9f) * Mathf.Cos((t - 0.12f) * 28f);
        popupRect.localScale = Vector3.one * scale;
        float alpha = t < popupHold ? 1f : 1f - (t - popupHold) / 0.4f;
        popupText.alpha = alpha;
        popupSub.alpha = alpha;
        if (alpha <= 0f) popupText.gameObject.SetActive(false);
    }

    void UpdateMarkers()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        var chests = Chest.Active;
        while (markers.Count < chests.Count) markers.Add(MakeMarker());
        for (int i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            bool show = i < chests.Count;
            m.root.gameObject.SetActive(show);
            if (!show) continue;
            var chest = chests[i];
            Color col = chest.IsJackpot ? new Color(1f, 0.35f, 0.25f) : gold;
            m.bubble.color = m.arrow.color = col;
            m.label.text = DebugCheats.Active && DebugCheats.ShowChestNumbers ? chest.Number.ToString() : (chest.IsJackpot ? "$" : "?");

            Vector3 vp = cam.WorldToViewportPoint(chest.transform.position + Vector3.up * 4.5f);
            bool behind = vp.z < 0f;
            bool onScreen = !behind && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f;
            Vector2 size = canvasRect.rect.size;
            if (onScreen)
            {
                m.arrow.gameObject.SetActive(false);
                m.root.anchoredPosition = new Vector2(vp.x * size.x, vp.y * size.y);
            }
            else
            {
                Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (behind) d = -d;
                if (d.sqrMagnitude < 1e-6f) d = Vector2.up;
                float k = Mathf.Min(0.44f / Mathf.Max(Mathf.Abs(d.x), 1e-4f), 0.40f / Mathf.Max(Mathf.Abs(d.y), 1e-4f));
                Vector2 edge = new Vector2(0.5f, 0.5f) + d * k;
                m.root.anchoredPosition = new Vector2(edge.x * size.x, edge.y * size.y);
                m.arrow.gameObject.SetActive(true);
                float angle = Mathf.Atan2(d.y * size.y, d.x * size.x) * Mathf.Rad2Deg - 90f;
                m.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                m.arrow.rectTransform.anchoredPosition = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 34f, 0f);
            }
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            m.bubble.rectTransform.localScale = Vector3.one * pulse;
        }
    }

    Marker MakeMarker()
    {
        var m = new Marker();
        m.root = NewRect("ChestMarker", markerLayer, Vector2.zero, new Vector2(10f, 10f));
        m.root.anchorMin = m.root.anchorMax = Vector2.zero;
        m.bubble = NewImage("Bubble", m.root, UISprites.Circle, gold);
        Anchor(m.bubble.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
        m.label = NewText("Label", m.bubble.rectTransform, "?", 26, new Color(0.15f, 0.1f, 0.05f));
        m.label.fontStyle = FontStyles.Bold;
        Anchor(m.label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
        m.arrow = NewImage("Arrow", m.root, UISprites.Triangle, gold);
        Anchor(m.arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(26f, 22f));
        return m;
    }

    // ---------------- tiny UI helpers ----------------

    public static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    public Material OutlinedText => outlinedText;

    CanvasGroup hudGroup;
    float hudAlphaTarget = 1f;
    /// <summary>Hides the gameplay HUD while the title screen is up; fades it back in.</summary>
    public void SetVisible(bool visible, bool instant = false)
    {
        hudAlphaTarget = visible ? 1f : 0f;
        if (hudGroup == null) return;
        if (instant) hudGroup.alpha = hudAlphaTarget;
        hudGroup.blocksRaycasts = visible;
    }

    void Outline(TextMeshProUGUI t)
    {
        if (outlinedText != null) t.fontSharedMaterial = outlinedText;
    }

    public static void Anchor(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    public static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
