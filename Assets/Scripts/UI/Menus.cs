using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Title screen, How to Play and the pause menu, built in code like the HUD.
/// On the title screen the camera orbits slowly over the city; PLAY swoops it down behind
/// Lucky 7 and then the 3-2-1 countdown starts. Esc (or P) pauses during a match.
/// </summary>
[DefaultExecutionOrder(200)] // after ChaseCamera, so the swoop can blend from its pose
public class Menus : MonoBehaviour
{
    [Header("Models shown in the menus (raw GLB models, no gameplay scripts)")]
    [SerializeField] GameObject carModel;
    [SerializeField] GameObject chestModel;
    [SerializeField] GameObject blasterModel;
    [SerializeField] GameObject cloverModel;
    [SerializeField] GameObject catModel;
    [SerializeField] GameObject trophyModel;

    [Header("Title camera")]
    [SerializeField] Vector3 orbitCentre = Vector3.zero;
    [SerializeField] float orbitRadius = 82f;
    [SerializeField] float orbitHeight = 44f;
    [SerializeField] float orbitSpeed = 3f;
    [SerializeField] float orbitFov = 50f;
    [SerializeField] float swoopTime = 2.1f;

    public static Menus Instance { get; private set; }
    public bool IsPaused { get; private set; }

    static readonly Color Panel = new Color(0.08f, 0.16f, 0.18f, 0.95f);
    static readonly Color PanelLight = new Color(0.12f, 0.22f, 0.24f, 0.96f);
    static readonly Color Cream = new Color(0.97f, 0.93f, 0.83f);
    static readonly Color Gold = new Color(1f, 0.8f, 0.18f);
    static readonly Color Teal = new Color(0.25f, 0.85f, 0.78f);
    static readonly Color Red = new Color(0.93f, 0.3f, 0.24f);
    static readonly Color Ink = new Color(0.14f, 0.09f, 0.04f);

    enum Page { None, Title, HowTo, Pause }
    Page page = Page.None;
    Page howToReturn = Page.Title;

    Canvas canvas;
    RectTransform root;
    CanvasGroup titleGroup, howToGroup, pauseGroup;
    RectTransform titleWord, titleCard;
    TextMeshProUGUI winsText;
    readonly List<Slider> musicSliders = new List<Slider>(), sfxSliders = new List<Slider>();
    float pageAge;

    Camera cam;
    ChaseCamera chase;
    float orbitAngle = -35f;
    bool orbiting, swooping;
    float swoopT;
    Vector3 swoopFromPos;
    Quaternion swoopFromRot;
    float swoopFromFov;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        cam = Camera.main;
        chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        int iconLayer = LayerMask.NameToLayer(ModelViewport.LayerName);
        if (cam != null && iconLayer >= 0) cam.cullingMask &= ~(1 << iconLayer);
        var tower = GameObject.Find("lucky_tower");
        if (tower != null) orbitCentre = new Vector3(tower.transform.position.x, 0f, tower.transform.position.z);

        BuildCanvas();
        BuildTitle();
        BuildHowTo();
        BuildPause();

        var mm = MatchManager.Instance;
        if (mm != null && mm.WaitingForMenu)
        {
            orbiting = true;
            if (chase != null) chase.enabled = false;
            if (Hud.Instance != null) Hud.Instance.SetVisible(false, true);
            Show(Page.Title);
        }
        else Show(Page.None);
    }

    // =====================================================================================
    // Flow
    // =====================================================================================

    void Show(Page p)
    {
        page = p;
        pageAge = 0f;
        SetGroup(titleGroup, p == Page.Title);
        SetGroup(howToGroup, p == Page.HowTo);
        SetGroup(pauseGroup, p == Page.Pause);
        if (winsText != null)
            winsText.text = MatchManager.Wins > 0 ? $"WINS  <color=#FFD033>{MatchManager.Wins}</color>" : "";
        SyncSliders();
    }

    static void SetGroup(CanvasGroup g, bool on)
    {
        g.gameObject.SetActive(on);
        g.alpha = on ? 0f : g.alpha;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    void Play()
    {
        if (swooping) return;
        Show(Page.None);
        orbiting = false;
        swooping = true;
        swoopT = 0f;
        swoopFromPos = cam.transform.position;
        swoopFromRot = cam.transform.rotation;
        swoopFromFov = cam.fieldOfView;
        if (chase != null) chase.enabled = true;
    }

    void FinishSwoop()
    {
        swooping = false;
        if (Hud.Instance != null) Hud.Instance.SetVisible(true);
        if (MatchManager.Instance != null) MatchManager.Instance.StartFromMenu();
    }

    void OpenHowTo(Page returnTo) { howToReturn = returnTo; Show(Page.HowTo); }

    void Pause(bool on)
    {
        IsPaused = on;
        Time.timeScale = on ? 0f : 1f;
        AudioListener.pause = on;
        Show(on ? Page.Pause : Page.None);
    }

    bool CanPause()
    {
        var mm = MatchManager.Instance;
        return mm != null && !mm.WaitingForMenu && !swooping && !mm.ResultsVisible && mm.State != MatchState.Ended;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        pageAge += dt;
        Fade(titleGroup, dt); Fade(howToGroup, dt); Fade(pauseGroup, dt);
        if (IsPaused && GameAudio.I != null) GameAudio.I.DuckMusic(0.2f, 0.45f);

        var k = Keyboard.current;
        if (k != null)
        {
            bool esc = k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame;
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            switch (page)
            {
                case Page.Title: if (enter && pageAge > 0.3f) { GameAudio.Click(); Play(); } break;
                case Page.HowTo: if (esc || enter) { GameAudio.Click(); Show(howToReturn); } break;
                case Page.Pause: if (esc) { GameAudio.Click(); Pause(false); } break;
                case Page.None: if (esc && CanPause()) { GameAudio.Click(); Pause(true); } break;
            }
        }

        // Title: the logo bobs and wobbles a little.
        if (page == Page.Title && titleWord != null)
        {
            float t = Time.unscaledTime;
            float intro = Mathf.Clamp01(pageAge / 0.5f);
            float pop = intro < 1f ? Mathf.Lerp(0.6f, 1f, 1f - Mathf.Pow(1f - intro, 3f)) + 0.08f * Mathf.Sin(intro * Mathf.PI) : 1f;
            titleWord.localScale = Vector3.one * (pop + 0.015f * Mathf.Sin(t * 2.1f));
            titleWord.localRotation = Quaternion.Euler(0f, 0f, 1.6f * Mathf.Sin(t * 1.3f));
        }
    }

    void Fade(CanvasGroup g, float dt)
    {
        if (!g.gameObject.activeSelf) return;
        g.alpha = Mathf.MoveTowards(g.alpha, 1f, dt * 5f);
    }

    void LateUpdate()
    {
        if (cam == null) return;
        float dt = Time.unscaledDeltaTime;
        if (orbiting)
        {
            orbitAngle += orbitSpeed * dt;
            Vector3 pos = orbitCentre + Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, orbitHeight, -orbitRadius);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(orbitCentre + Vector3.up * 6f - pos));
            cam.fieldOfView = orbitFov;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 420f);
        }
        else if (swooping)
        {
            swoopT += dt / swoopTime;
            float t = Mathf.Clamp01(swoopT);
            float e = t * t * t * (t * (6f * t - 15f) + 10f); // smootherstep
            // The chase camera has just placed the camera where it wants to be this frame.
            Vector3 toPos = cam.transform.position;
            Quaternion toRot = cam.transform.rotation;
            float toFov = cam.fieldOfView;

            // Glide across first and drop late, so the camera stays above the rooftops.
            Vector3 pos = Vector3.Lerp(swoopFromPos, toPos, e);
            pos.y = Mathf.Lerp(swoopFromPos.y, toPos.y, Mathf.Pow(e, 2.2f));
            var player = CarIdentity.Player;
            Vector3 focus = player != null ? player.transform.position + Vector3.up * 1.4f : toPos + toRot * Vector3.forward * 10f;
            Vector3 lookAt = Vector3.Lerp(orbitCentre + Vector3.up * 6f, focus, Mathf.Clamp01(e * 1.6f));
            Quaternion rot = Quaternion.LookRotation(lookAt - pos);
            rot = Quaternion.Slerp(rot, toRot, Mathf.Pow(e, 3f));
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = Mathf.Lerp(swoopFromFov, toFov, e);
            if (t >= 1f) FinishSwoop();
        }
    }

    // =====================================================================================
    // Building
    // =====================================================================================

    void BuildCanvas()
    {
        var go = new GameObject("Menus Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)go.transform;
    }

    CanvasGroup NewPage(string name, Color dim)
    {
        var bg = Hud.NewImage(name, root, null, dim);
        Hud.Stretch(bg.rectTransform);
        bg.raycastTarget = dim.a > 0f;
        var g = bg.gameObject.AddComponent<CanvasGroup>();
        bg.gameObject.SetActive(false);
        return g;
    }

    void BuildTitle()
    {
        titleGroup = NewPage("Title", new Color(0f, 0f, 0f, 0f));
        var r = (RectTransform)titleGroup.transform;

        // Soft dark gradient at the bottom so the buttons read over the city.
        var shade = Hud.NewImage("BottomShade", r, UISprites.Vignette, new Color(0.03f, 0.05f, 0.07f, 0.55f));
        Hud.Stretch(shade.rectTransform);

        titleWord = Hud.NewRect("Logo", r, Vector2.zero, new Vector2(900f, 220f));
        Hud.Anchor(titleWord, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 220f));
        var jackpot = Text(titleWord, "JACKPOT", 118, Gold, new Vector2(0f, 42f), new Vector2(900f, 130f), true);
        jackpot.enableVertexGradient = true;
        jackpot.colorGradient = new VertexGradient(new Color(1f, 0.93f, 0.45f), new Color(1f, 0.93f, 0.45f), new Color(1f, 0.6f, 0.12f), new Color(1f, 0.6f, 0.12f));
        jackpot.color = Color.white;
        jackpot.characterSpacing = 4f;
        var derby = Text(titleWord, "DERBY", 86, Color.white, new Vector2(0f, -58f), new Vector2(900f, 100f), true);
        derby.enableVertexGradient = true;
        derby.colorGradient = new VertexGradient(new Color(0.55f, 1f, 0.92f), new Color(0.55f, 1f, 0.92f), new Color(0.15f, 0.7f, 0.68f), new Color(0.15f, 0.7f, 0.68f));
        derby.characterSpacing = 18f;

        var tag = Text(r, "Roll your lucky number.  Crack the chests.  Be the last car standing.", 22, Cream,
                       Vector2.zero, new Vector2(1000f, 32f), true);
        Hud.Anchor(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(1000f, 32f));

        // Buttons
        var col = Hud.NewRect("Buttons", r, Vector2.zero, new Vector2(320f, 200f));
        Hud.Anchor(col, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(320f, 200f));
        MakeButton(col, "PLAY", Gold, Ink, new Vector2(0f, 50f), new Vector2(300f, 78f), 40, Play, "press ENTER");
        MakeButton(col, "HOW TO PLAY", Teal, Ink, new Vector2(0f, -45f), new Vector2(260f, 56f), 26, () => OpenHowTo(Page.Title));

        winsText = Text(r, "", 22, Cream, Vector2.zero, new Vector2(300f, 30f), true);
        winsText.richText = true;
        Hud.Anchor(winsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 30f));

        // Sound settings, bottom-left.
        var vol = Hud.NewImage("Volume", r, UISprites.RoundedRect, new Color(0.08f, 0.16f, 0.18f, 0.8f));
        Hud.Anchor(vol.rectTransform, new Vector2(0f, 0f), new Vector2(160f, 62f), new Vector2(280f, 84f));
        MakeVolumeRow(vol.rectTransform, "MUSIC", new Vector2(0f, 16f), true);
        MakeVolumeRow(vol.rectTransform, "SOUND", new Vector2(0f, -18f), false);

        var credit = Text(r, "Unity Game Jam 2026  ·  theme: Lucky Number", 15, new Color(1f, 1f, 1f, 0.7f), Vector2.zero, new Vector2(500f, 22f), true);
        credit.alignment = TextAlignmentOptions.Right;
        Hud.Anchor(credit.rectTransform, new Vector2(1f, 0f), new Vector2(-270f, 30f), new Vector2(500f, 22f));
    }

    void BuildHowTo()
    {
        howToGroup = NewPage("HowToPlay", new Color(0.02f, 0.05f, 0.06f, 0.72f));
        var r = (RectTransform)howToGroup.transform;

        var head = Text(r, "HOW TO PLAY", 54, Gold, Vector2.zero, new Vector2(800f, 64f), true);
        Hud.Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(800f, 64f));

        var grid = Hud.NewRect("Cards", r, Vector2.zero, new Vector2(1180f, 500f));
        Hud.Anchor(grid, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1180f, 500f));

        Card(grid, 0, 0, carModel, Teal, "DRIVE",
             "<b>W A S D</b> or <b>arrow keys</b> to drive, brake, reverse and steer.\n<b>SHIFT</b> = Turbo: a burst of speed and fire rate.");
        Card(grid, 1, 0, null, Gold, "YOUR LUCKY NUMBER",
             "Your number (1-99) rolls <b>up</b> as you drive forward and <b>down</b> in reverse.\n<b>Stop</b> to lock it and get a hint: TOO HIGH, TOO LOW, CLOSE or LUCKY!  <b>R</b> unlocks it.", true);
        Card(grid, 2, 0, chestModel, Gold, "CHESTS",
             "Each chest hides a number. <b>Ram it</b> with a number within 5 of it to crack it open.\nToo far off and you bounce. Spot on = <b>LUCKY!</b> and a full repair.");
        Card(grid, 0, 1, blasterModel, Teal, "BLASTERS",
             "Chests upgrade you: <b>Small Blaster</b>, then the <b>Jackpot Cannon</b>.\nThey fire by themselves at the nearest enemy in range. You just drive.");
        Card(grid, 1, 1, cloverModel, new Color(0.45f, 0.95f, 0.45f), "LUCK & BAD LUCK",
             "<b>Clovers</b> repair you. <b>Dice</b> re-roll your number. <b>Black cats</b> scramble it!\nStay out of the purple <b>Bad Luck Storm</b>: it closes in.");
        Card(grid, 2, 1, trophyModel, Red, "LAST CAR STANDING",
             "Blast and ram the other three cars. Side and rear rams hit hardest.\nUnarmed? Run, hide and hunt for chests. <b>Last car alive wins!</b>");

        MakeButton(r, "BACK", Teal, Ink, Vector2.zero, new Vector2(220f, 54f), 26, () => Show(howToReturn), null, new Vector2(0.5f, 0f), new Vector2(0f, 52f));
    }

    void Card(RectTransform grid, int cx, int cy, GameObject model, Color accent, string title, string body, bool reels = false)
    {
        const float W = 376f, H = 238f;
        var card = Hud.NewImage("Card_" + title, grid, UISprites.RoundedRect, PanelLight);
        Hud.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((cx - 1) * (W + 16f), (0.5f - cy) * (H + 16f)), new Vector2(W, H));
        var stripe = Hud.NewImage("Accent", card.rectTransform, UISprites.RoundedRect, accent);
        Hud.Anchor(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(W - 16f, 6f));

        var iconBack = Hud.NewImage("IconBack", card.rectTransform, UISprites.Circle, new Color(accent.r, accent.g, accent.b, 0.18f));
        Hud.Anchor(iconBack.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -66f), new Vector2(100f, 100f));
        if (reels) MiniReels(iconBack.rectTransform);
        else if (model != null)
        {
            var img = ModelViewport.Create(iconBack.rectTransform, model, new Vector2(118f, 118f), 35f, 30f, 16f, 1.05f);
            Hud.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));
        }

        var t = Text(card.rectTransform, title, 24, accent, Vector2.zero, new Vector2(240f, 60f), true);
        t.alignment = TextAlignmentOptions.Left;
        t.textWrappingMode = TextWrappingModes.Normal;
        Hud.Anchor(t.rectTransform, new Vector2(0f, 1f), new Vector2(250f, -66f), new Vector2(240f, 60f));

        body = body.Replace("<b>", "<b><color=#FFE08A>").Replace("</b>", "</color></b>");
        var b = Text(card.rectTransform, body, 17f, Cream, Vector2.zero, new Vector2(W - 36f, 110f), false);
        b.fontStyle = FontStyles.Normal;
        b.alignment = TextAlignmentOptions.TopLeft;
        b.textWrappingMode = TextWrappingModes.Normal;
        b.richText = true;
        b.lineSpacing = 4f;
        Hud.Anchor(b.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(W - 36f, 110f));
    }

    /// <summary>Two little slot-reel digits showing 07, like the HUD odometer.</summary>
    void MiniReels(RectTransform parent)
    {
        for (int i = 0; i < 2; i++)
        {
            var w = Hud.NewImage("Reel", parent, UISprites.RoundedRect, new Color(1f, 0.82f, 0.25f));
            Hud.Anchor(w.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-19f + i * 38f, 0f), new Vector2(34f, 50f));
            var d = Hud.NewText("D", w.rectTransform, i == 0 ? "0" : "7", 40, new Color(0.12f, 0.12f, 0.14f));
            d.fontStyle = FontStyles.Bold;
            Hud.Stretch(d.rectTransform);
        }
    }

    void BuildPause()
    {
        pauseGroup = NewPage("Pause", new Color(0.02f, 0.04f, 0.06f, 0.6f));
        var r = (RectTransform)pauseGroup.transform;
        var card = Hud.NewImage("Card", r, UISprites.RoundedRect, Panel);
        Hud.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 470f));
        var c = card.rectTransform;
        var head = Text(c, "PAUSED", 52, Gold, Vector2.zero, new Vector2(400f, 64f), true);
        Hud.Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(400f, 64f));

        MakeButton(c, "RESUME", Gold, Ink, new Vector2(0f, 105f), new Vector2(300f, 60f), 30, () => Pause(false), "press ESC");
        MakeButton(c, "RESTART", Teal, Ink, new Vector2(0f, 35f), new Vector2(260f, 48f), 22, () =>
        {
            Pause(false);
            if (MatchManager.Instance != null) MatchManager.Instance.PlayAgain();
        });
        MakeButton(c, "HOW TO PLAY", Teal, Ink, new Vector2(0f, -22f), new Vector2(260f, 48f), 22, () => OpenHowTo(Page.Pause));
        MakeButton(c, "MAIN MENU", new Color(0.85f, 0.85f, 0.8f), Ink, new Vector2(0f, -79f), new Vector2(260f, 48f), 22, () =>
        {
            Pause(false);
            if (MatchManager.Instance != null) MatchManager.Instance.BackToMenu();
        });
        MakeVolumeRow(c, "MUSIC", new Vector2(0f, -140f), true);
        MakeVolumeRow(c, "SOUND", new Vector2(0f, -178f), false);
    }

    // =====================================================================================
    // Widgets
    // =====================================================================================

    TextMeshProUGUI Text(RectTransform parent, string s, float size, Color color, Vector2 pos, Vector2 box, bool outlined)
    {
        var t = Hud.NewText("Text", parent, s, size, color);
        t.fontStyle = FontStyles.Bold;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        if (outlined && Hud.Instance != null && Hud.Instance.OutlinedText != null) t.fontSharedMaterial = Hud.Instance.OutlinedText;
        return t;
    }

    public Button MakeButton(RectTransform parent, string label, Color fill, Color ink, Vector2 pos, Vector2 size, float fontSize,
                             Action onClick, string hint = null, Vector2? anchor = null, Vector2? anchoredPos = null)
    {
        var shadow = Hud.NewImage("Btn_" + label, parent, UISprites.RoundedRect, new Color(0f, 0f, 0f, 0.35f));
        Hud.Anchor(shadow.rectTransform, anchor ?? new Vector2(0.5f, 0.5f), anchoredPos ?? pos, size);
        shadow.raycastTarget = true;
        var face = Hud.NewImage("Face", shadow.rectTransform, UISprites.RoundedRect, fill);
        Hud.Anchor(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), size);
        var shine = Hud.NewImage("Shine", face.rectTransform, UISprites.RoundedRect, new Color(1f, 1f, 1f, 0.22f));
        Hud.Anchor(shine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.22f), new Vector2(size.x - 14f, size.y * 0.32f));
        var t = Hud.NewText("Label", face.rectTransform, label, fontSize, ink);
        t.fontStyle = FontStyles.Bold;
        Hud.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, hint != null ? 5f : 1f), size);
        if (hint != null)
        {
            var h = Hud.NewText("Hint", face.rectTransform, hint, 12, new Color(ink.r, ink.g, ink.b, 0.7f));
            Hud.Anchor(h.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 11f), new Vector2(size.x, 16f));
        }
        var b = shadow.gameObject.AddComponent<Button>();
        b.targetGraphic = face;
        var cb = b.colors;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.fadeDuration = 0.06f;
        b.colors = cb;
        b.onClick.AddListener(() => onClick());
        shadow.gameObject.AddComponent<JuicyButton>();
        return b;
    }

    void MakeVolumeRow(RectTransform parent, string label, Vector2 pos, bool music)
    {
        var row = Hud.NewRect("Vol_" + label, parent, pos, new Vector2(260f, 28f));
        Hud.Anchor(row, new Vector2(0.5f, 0.5f), pos, new Vector2(260f, 28f));
        var t = Hud.NewText("Label", row, label, 15, Cream);
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Left;
        Hud.Anchor(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(80f, 24f));

        var area = Hud.NewImage("Track", row, UISprites.RoundedRect, new Color(0.03f, 0.05f, 0.06f, 0.9f));
        Hud.Anchor(area.rectTransform, new Vector2(1f, 0.5f), new Vector2(-88f, 0f), new Vector2(170f, 12f));
        area.raycastTarget = true;
        var fillArea = Hud.NewRect("FillArea", area.rectTransform, Vector2.zero, Vector2.zero);
        Hud.Stretch(fillArea);
        var fill = Hud.NewImage("Fill", fillArea, UISprites.RoundedRect, music ? Gold : Teal);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        var handleArea = Hud.NewRect("HandleArea", area.rectTransform, Vector2.zero, Vector2.zero);
        Hud.Stretch(handleArea);
        var handle = Hud.NewImage("Handle", handleArea, UISprites.Circle, Cream);
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);
        handle.raycastTarget = true;

        var s = area.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;
        s.value = GameAudio.I != null ? (music ? GameAudio.I.MusicVolume : GameAudio.I.SfxVolume) : 0.8f;
        s.onValueChanged.AddListener(v =>
        {
            if (GameAudio.I == null) return;
            if (music) GameAudio.I.MusicVolume = v;
            else { GameAudio.I.SfxVolume = v; GameAudio.Hover(); }
            SyncSliders();
        });
        (music ? musicSliders : sfxSliders).Add(s);
    }

    void SyncSliders()
    {
        if (GameAudio.I == null) return;
        foreach (var s in musicSliders) s.SetValueWithoutNotify(GameAudio.I.MusicVolume);
        foreach (var s in sfxSliders) s.SetValueWithoutNotify(GameAudio.I.SfxVolume);
    }

    /// <summary>A spinning trophy for the results screen (1st place).</summary>
    public RawImage MakeTrophy(RectTransform parent, Vector2 size)
    {
        return ModelViewport.Create(parent, trophyModel, size, 0f, 70f, 10f, 1f, 0.04f);
    }
}
