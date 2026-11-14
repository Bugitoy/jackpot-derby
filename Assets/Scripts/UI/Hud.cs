using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's HUD, built in code at startup from Lefika's painted UI art (see UIKit): the
/// three-reel odometer frame with its padlock and luck slot, hint line, big popups (TOO HIGH,
/// LUCKY!, SMALL BLASTER!...), blaster status, and chest markers/arrows.
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

    const float DigitH = 74f;
    // The odometer frame art and where its painted windows, luck slot and padlock sit (canvas units).
    static readonly Vector2 FrameSize = new Vector2(288f, 140f);
    static readonly float[] ReelX = { -67.8f, -8.4f, 51.3f };
    const float ReelY = 13.3f;
    static readonly Vector2 ReelSize = new Vector2(50f, DigitH);
    static readonly Vector2 SlotPos = new Vector2(-6.9f, -41.7f);
    static readonly Vector2 PadlockTop = new Vector2(125.6f, -24.4f);
    static readonly Vector2 PadlockSize = new Vector2(41.3f, 40.8f);

    Canvas canvas;
    RectTransform canvasRect;
    Camera cam;
    CarIdentity player;
    Odometer odometer;
    CarLoadout loadout;

    RectTransform hundredsColumn, tensColumn, onesColumn;
    Image hundredsWindow, tensWindow, onesWindow, padlock, padGlow;
    float padAngle, padSpin;
    TextMeshProUGUI hintLine, popupText, popupSub, cheatLine;
    RectTransform popupRect;
    float popupAge = 99f;
    float shownValue;
    int lastNumber;
    float lockPunch;

    // Dice luck: pips in the odometer panel and the slot-machine spin of the reels.
    DiceLuck luck;
    readonly Image[] luckPips = new Image[DiceLuck.MaxStacks];
    readonly float[] pipPop = new float[DiceLuck.MaxStacks];
    TextMeshProUGUI luckChance;
    int shownStacks;
    float spinAge = -1f, spinDur = 1f;
    int spinTo;

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
        luck = DiceLuck.For(player.gameObject);
        luck.RollStarted += OnDiceRoll;
        luck.RollLanded += OnDiceLanded;
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

        // Odometer, bottom centre: the painted three-reel frame. Reels sit in its windows, the luck
        // pips in its lower slot, and the padlock (cut out of the art) swings from its chain.
        var frame = UIKit.ArtImage(canvasRect, "odometer_frame", new Vector2(0.5f, 0f), new Vector2(0f, 80f), FrameSize.y, FrameSize.x);
        var fr = frame.rectTransform;
        // Reels sit in the painted windows (which are dark paint, not holes), inset a little so the
        // window's rim shows round each drum.
        var reels = NewRect("Reels", fr, Vector2.zero, FrameSize);
        Anchor(reels, new Vector2(0.5f, 0.5f), Vector2.zero, FrameSize);
        hundredsWindow = MakeReel(reels, new Vector2(ReelX[0], ReelY), out hundredsColumn);
        tensWindow = MakeReel(reels, new Vector2(ReelX[1], ReelY), out tensColumn);
        onesWindow = MakeReel(reels, new Vector2(ReelX[2], ReelY), out onesColumn);

        // Luck slot: LUCK [] [] [] [] [] 20%
        var slot = NewRect("LuckSlot", fr, SlotPos, new Vector2(196f, 22f));
        Anchor(slot, new Vector2(0.5f, 0.5f), SlotPos, new Vector2(196f, 22f));
        var luckLabel = UIKit.Label(slot, "LUCK", 11f, gold, UIWeight.ExtraBold);
        luckLabel.alignment = TextAlignmentOptions.Left;
        Anchor(luckLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(44f, 20f));
        for (int i = 0; i < luckPips.Length; i++)
        {
            luckPips[i] = NewImage("LuckPip" + i, slot, UISprites.RoundedRect, new Color(1f, 1f, 1f, 0.12f));
            Anchor(luckPips[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-30f + i * 17f, 0f), new Vector2(13f, 12f));
            luckPips[i].pixelsPerUnitMultiplier = 5f; // tight corners: little dice
            var dot = NewImage("Dot", luckPips[i].rectTransform, UISprites.Circle, new Color(0.15f, 0.1f, 0.05f, 0f));
            Anchor(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f));
        }
        luckChance = UIKit.Label(slot, "", 11f, gold, UIWeight.ExtraBold);
        luckChance.alignment = TextAlignmentOptions.Right;
        Anchor(luckChance.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(44f, 20f));

        padGlow = NewImage("PadlockGlow", fr, UISprites.Glow, new Color(1f, 0.8f, 0.25f, 0f));
        Anchor(padGlow.rectTransform, new Vector2(0.5f, 0.5f), PadlockTop + new Vector2(0f, -PadlockSize.y * 0.55f), PadlockSize * 2.2f);
        padlock = NewImage("Padlock", fr, UIKit.Art("odometer_padlock"), Color.white);
        padlock.preserveAspect = true;
        var pr = padlock.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 1f);           // hangs from the chain
        pr.anchoredPosition = PadlockTop;
        pr.sizeDelta = PadlockSize;

        hintLine = NewText("HintLine", canvasRect, "", 26, Color.white);
        Anchor(hintLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 172f), new Vector2(500f, 34f));
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
        var window = NewImage("Reel", parent, null, reelColor);
        Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), pos, ReelSize);
        window.gameObject.AddComponent<RectMask2D>();
        column = NewRect("Column", window.rectTransform, Vector2.zero, new Vector2(ReelSize.x, DigitH * 11));
        column.anchorMin = column.anchorMax = new Vector2(0.5f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        for (int i = 0; i <= 10; i++)
        {
            var t = UIKit.Label(column, (i % 10).ToString(), 60f, digitColor, UIWeight.ExtraBold);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -i * DigitH + 3f);
            r.sizeDelta = new Vector2(ReelSize.x, DigitH);
        }
        // Drum shading: darker at the top and bottom where it curves away, a soft sheen in the middle.
        for (int k = 0; k < 2; k++)
        {
            var shade = NewImage(k == 0 ? "ShadeTop" : "ShadeBottom", window.rectTransform, null, new Color(0.15f, 0.1f, 0.05f, 0.28f));
            Anchor(shade.rectTransform, new Vector2(0.5f, k == 0 ? 1f : 0f), new Vector2(0f, k == 0 ? -8f : 8f), new Vector2(ReelSize.x, 16f));
            var shade2 = NewImage("Shade2", window.rectTransform, null, new Color(0.15f, 0.1f, 0.05f, 0.16f));
            Anchor(shade2.rectTransform, new Vector2(0.5f, k == 0 ? 1f : 0f), new Vector2(0f, k == 0 ? -20f : 20f), new Vector2(ReelSize.x, 8f));
        }
        var sheen = NewImage("Sheen", window.rectTransform, UISprites.Glow, new Color(1f, 1f, 1f, 0.22f));
        Anchor(sheen.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(ReelSize.x * 1.4f, 34f));
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

        float ones, tens, hundreds = 0f;
        if (spinAge >= 0f)
        {
            // Slot-machine spin after a dice: reels stop left to right, the last one thunks in.
            spinAge += Time.deltaTime;
            float tH = Mathf.Clamp01(spinAge / (spinDur * 0.45f));
            float tT = Mathf.Clamp01(spinAge / (spinDur * 0.7f)), tO = Mathf.Clamp01(spinAge / spinDur);
            int tensTo = spinTo / 10, onesTo = spinTo % 10;
            hundreds = Mathf.Repeat(Mathf.LerpUnclamped(-10f, 0f, EaseOutBack(tH)), 10f);
            tens = Mathf.Repeat(Mathf.LerpUnclamped(tensTo - 20f, tensTo, EaseOutBack(tT)), 10f);
            ones = Mathf.Repeat(Mathf.LerpUnclamped(onesTo - 30f, onesTo, EaseOutBack(tO)), 10f);
            if (tT >= 1f && tO < 1f && spinAge - Time.deltaTime < spinDur * 0.7f) lockPunch = 0.6f;
            if (tO >= 1f) { spinAge = -1f; lockPunch = 1f; }
            shownValue = n;
        }
        else
        {
            float target = n + odometer.Progress;
            shownValue = Mathf.Lerp(shownValue, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            float v = Mathf.Repeat(shownValue, 100f);
            ones = Mathf.Repeat(v, 10f);
            tens = Mathf.Floor(v / 10f) + Mathf.Clamp01(ones - 9f);
        }
        onesColumn.anchoredPosition = new Vector2(0f, ones * DigitH);
        tensColumn.anchoredPosition = new Vector2(0f, tens * DigitH);
        hundredsColumn.anchoredPosition = new Vector2(0f, hundreds * DigitH);
        UpdateLuckPips();

        bool locked = odometer.IsLocked;
        float dt = Time.deltaTime;
        lockPunch = Mathf.Max(0f, lockPunch - dt * 4f);
        Color w = locked ? reelLockedColor : reelColor;
        hundredsWindow.color = tensWindow.color = onesWindow.color = w;
        float s = 1f + 0.08f * Mathf.Sin(lockPunch * Mathf.PI);
        foreach (var win in new[] { hundredsWindow, tensWindow, onesWindow }) win.rectTransform.localScale = new Vector3(1f, s, 1f);

        // The padlock swings on its chain (a damped spring) and glows gold while locked.
        float rest = locked ? 0f : 9f;
        padSpin += ((rest - padAngle) * 120f - padSpin * 7f) * dt;
        padAngle += padSpin * dt;
        padlock.rectTransform.localRotation = Quaternion.Euler(0f, 0f, padAngle);
        padlock.color = Color.Lerp(padlock.color, locked ? Color.white : new Color(0.62f, 0.62f, 0.66f), 1f - Mathf.Exp(-10f * dt));
        float glow = locked ? 0.55f + 0.12f * Mathf.Sin(Time.time * 4f) + 0.4f * lockPunch : 0f;
        padGlow.color = new Color(1f, 0.8f, 0.25f, Mathf.MoveTowards(padGlow.color.a, glow, dt * 3f));
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.1f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void UpdateLuckPips()
    {
        if (luck == null) return;
        int s = luck.Stacks;
        if (s > shownStacks) for (int i = shownStacks; i < s; i++) pipPop[i] = 1f;
        shownStacks = s;
        float dt = Time.deltaTime;
        for (int i = 0; i < luckPips.Length; i++)
        {
            bool on = i < s;
            pipPop[i] = Mathf.Max(0f, pipPop[i] - dt * 3f);
            luckPips[i].color = on ? Color.Lerp(gold, Color.white, pipPop[i]) : new Color(1f, 1f, 1f, 0.12f);
            luckPips[i].transform.GetChild(0).GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.05f, on ? 0.85f : 0f);
            luckPips[i].rectTransform.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(pipPop[i] * Mathf.PI));
        }
        luckChance.text = s >= DiceLuck.MaxStacks ? "MAX" : Mathf.RoundToInt(luck.NextChance * 100f) + "%";
        luckChance.color = s > 0 ? gold : new Color(0.75f, 0.9f, 0.88f, 0.6f);
    }

    void OnDiceRoll(int stacks, bool hadChest)
    {
        spinTo = odometer != null ? odometer.Number : 0;
        spinDur = luck != null ? luck.SpinTime : 1f;
        spinAge = 0f;
    }

    void OnDiceLanded(HintResult h, bool exact, int stacks)
    {
        if (exact) ShowPopup("LUCKY ROLL!", gold, "the chest's exact number - go ram it!");
        else if (h.OpensChest) ShowPopup("CLOSE!", h.Color, "close enough - go ram the chest!");
        else ShowPopup(h.Text, h.Color, "so close! more dice, better odds");
    }

    void OnLockChanged(bool locked)
    {
        if (locked) lockPunch = 1f;
        padSpin += locked ? -260f : 180f;   // clunk: the padlock swings as it snaps shut / falls open
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
        var poppins = UIKit.Font(UIWeight.Bold);
        if (poppins != null) t.font = poppins;
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
        var m = UIKit.Outlined(t.font);
        if (m == null) m = outlinedText;
        if (m != null) t.fontSharedMaterial = m;
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
