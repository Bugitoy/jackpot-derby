using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Title screen, How to Play and the pause menu, built in code from Lefika's painted menu art
/// (see UIKit / MenuArtBuilder) to match the concept screens in Docs/Menu_Assets.
/// On the title screen the camera orbits slowly over the city; PLAY swoops it down behind
/// Lucky 7 and then the 3-2-1 countdown starts. Esc (or P) pauses during a match; the pause menu
/// and How to Play blur and darken the game behind them.
/// </summary>
[DefaultExecutionOrder(200)] // after ChaseCamera, so the swoop can blend from its pose
public class Menus : MonoBehaviour
{
    [Header("Models shown in the menus (no gameplay scripts)")]
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

    // Ink colours for the painted buttons (dark text on each paint colour, as in the concepts).
    static readonly Color InkYellow = new Color(0.16f, 0.1f, 0.03f);
    static readonly Color InkTeal = new Color(0.04f, 0.17f, 0.2f);
    static readonly Color InkSlate = new Color(0.08f, 0.16f, 0.17f);
    static readonly Color InkCream = new Color(0.17f, 0.13f, 0.09f);
    static readonly Color SteelTeal = new Color(0.42f, 0.86f, 0.86f);

    enum Page { None, Title, HowTo, Pause }
    Page page = Page.None;
    Page howToReturn = Page.Title;

    Canvas canvas;
    RectTransform root;
    CanvasGroup titleGroup, howToGroup, pauseGroup;
    RectTransform logo;
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

    Volume blurVolume;

    void Awake() => Instance = this;
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (blurVolume != null && blurVolume.profile != null) Destroy(blurVolume.profile);
    }

    void Start()
    {
        cam = Camera.main;
        chase = cam != null ? cam.GetComponent<ChaseCamera>() : null;
        int iconLayer = LayerMask.NameToLayer(ModelViewport.LayerName);
        if (cam != null && iconLayer >= 0) cam.cullingMask &= ~(1 << iconLayer);
        var tower = GameObject.Find("lucky_tower");
        if (tower != null) orbitCentre = new Vector3(tower.transform.position.x, 0f, tower.transform.position.z);

        BuildBlur();
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
        if (winsText != null) winsText.text = $"WINS  {MatchManager.Wins}";
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

        // Blur the game behind the pause menu and How to Play.
        if (blurVolume != null)
            blurVolume.weight = Mathf.MoveTowards(blurVolume.weight, page == Page.Pause || page == Page.HowTo ? 1f : 0f, dt * 5f);

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

        // Title: the logo pops in, then bobs and wobbles a little.
        if (page == Page.Title && logo != null)
        {
            float t = Time.unscaledTime;
            float intro = Mathf.Clamp01(pageAge / 0.5f);
            float pop = intro < 1f ? Mathf.Lerp(0.6f, 1f, 1f - Mathf.Pow(1f - intro, 3f)) + 0.08f * Mathf.Sin(intro * Mathf.PI) : 1f;
            logo.localScale = Vector3.one * (pop + 0.012f * Mathf.Sin(t * 2.1f));
            logo.localRotation = Quaternion.Euler(0f, 0f, 1.2f * Mathf.Sin(t * 1.3f));
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

    /// <summary>A global post-processing volume that blurs and darkens the game behind menus.</summary>
    void BuildBlur()
    {
        var go = new GameObject("MenuBlurVolume");
        go.transform.SetParent(transform, false);
        blurVolume = go.AddComponent<Volume>();
        blurVolume.isGlobal = true;
        blurVolume.priority = 50f;
        blurVolume.weight = 0f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(0f);
        dof.gaussianEnd.Override(2f);
        dof.gaussianMaxRadius.Override(1.5f);
        dof.highQualitySampling.Override(true);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-0.55f);
        color.saturation.Override(-25f);
        blurVolume.profile = profile;
    }

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

    // ---------------- title ----------------

    void BuildTitle()
    {
        titleGroup = NewPage("Title", new Color(0f, 0f, 0f, 0f));
        var r = (RectTransform)titleGroup.transform;

        // Soft dark edges so the logo and buttons read over the city.
        var shade = Hud.NewImage("EdgeShade", r, UISprites.Vignette, new Color(0.03f, 0.04f, 0.06f, 0.5f));
        Hud.Stretch(shade.rectTransform);

        var logoImg = UIKit.ArtImage(r, "logo", new Vector2(0.5f, 1f), new Vector2(0f, -146f), 202f);
        logoImg.preserveAspect = true;
        logo = logoImg.rectTransform;

        var tag = UIKit.Label(r, "Roll your lucky number.  Crack the chests.  Be the last car standing.", 22f, UIKit.Cream, UIWeight.Bold, true);
        Hud.Anchor(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1100f, 34f));

        UIKit.PaintedButton(r, "btn_yellow_big", "PLAY", InkYellow, new Vector2(0.5f, 0f), new Vector2(0f, 232f), new Vector2(306f, 98f), 50f, Play);
        UIKit.PaintedButton(r, "btn_teal_big", "HOW TO PLAY", InkTeal, new Vector2(0.5f, 0f), new Vector2(0f, 141f), new Vector2(264f, 70f), 29f, () => OpenHowTo(Page.Title));

        var plaque = UIKit.ArtImage(r, "plaque", new Vector2(0.5f, 0f), new Vector2(0f, 58f), 48f);
        winsText = UIKit.Label(plaque.rectTransform, "WINS  0", 23f, InkCream, UIWeight.ExtraBold);
        Hud.Stretch(winsText.rectTransform);
        winsText.rectTransform.anchoredPosition = new Vector2(0f, 1f);

        // Sound settings, bottom-left, on a steel plate.
        var vol = UIKit.ArtImage(r, "steel_plate", new Vector2(0f, 0f), new Vector2(160f, 58f), 104f, 292f);
        AddVolumeSliders(vol.rectTransform, new Vector2(0f, 19f), new Vector2(0f, -19f), 74f, 172f, 21f);

        var credit = UIKit.Label(r, "Unity Game Jam 2026  •  theme: Lucky Number", 16f, Color.white, UIWeight.SemiBold, true);
        credit.alignment = TextAlignmentOptions.Right;
        Hud.Anchor(credit.rectTransform, new Vector2(1f, 0f), new Vector2(-268f, 28f), new Vector2(520f, 24f));
    }

    // ---------------- how to play ----------------

    void BuildHowTo()
    {
        howToGroup = NewPage("HowToPlay", new Color(0.02f, 0.03f, 0.04f, 0.45f));
        var r = (RectTransform)howToGroup.transform;

        var head = UIKit.Label(r, "HOW TO PLAY", 64f, Color.white, UIWeight.ExtraBold, true);
        UIKit.GoldGradient(head);
        Hud.Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(900f, 80f));

        var grid = Hud.NewRect("Cards", r, Vector2.zero, new Vector2(1180f, 480f));
        Hud.Anchor(grid, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1180f, 480f));

        Color blue = new Color(0.36f, 0.62f, 0.86f), orange = new Color(0.96f, 0.5f, 0.22f), yellow = new Color(1f, 0.8f, 0.22f);
        Color green = new Color(0.36f, 0.8f, 0.4f), red = new Color(0.93f, 0.3f, 0.24f);
        Card(grid, 0, 0, carModel, blue, blue, "DRIVE",
             "<b>W A S D</b> or <b>arrow keys</b> to drive, brake, reverse and steer. <b>SHIFT</b> = Turbo: more speed and fire rate.");
        Card(grid, 1, 0, null, orange, UIKit.Gold, "YOUR NUMBER",
             "Your number (1-99) rolls <b>up</b> as you drive forward and <b>down</b> in reverse. <b>Stop</b> to lock it and get a hint: TOO HIGH, TOO LOW, CLOSE or LUCKY! <b>R</b> unlocks it.", true);
        Card(grid, 2, 0, chestModel, yellow, UIKit.Gold, "CHESTS",
             "Each chest hides a number. <b>Ram it</b> with a number within 5 of it to crack it open. Too far off and you bounce. Spot on = <b>LUCKY!</b> and a full repair.");
        Card(grid, 0, 1, blasterModel, blue, blue, "BLASTERS",
             "Chests upgrade your cannon: <b>Small Blaster</b>, then the <b>Jackpot Cannon</b>. They fire by themselves at the nearest enemy in range.");
        Card(grid, 1, 1, cloverModel, green, green, "LUCK",
             "Each <b>Dice</b> raises your odds of rolling a chest's exact number. <b>Black cats</b> wipe it! <b>Clovers</b> repair you. Avoid the purple <b>Storm</b>.");
        Card(grid, 2, 1, trophyModel, red, red, "LAST CAR STANDING",
             "Blast and ram the other three cars. Side and rear rams hit hardest. Unarmed? Run, hide and hunt for chests. <b>Last car alive wins!</b>");

        var back = UIKit.PaintedButton(r, "steel_plate", "BACK", SteelTeal, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(214f, 58f), 31f,
                                       () => Show(howToReturn), null, UIWeight.ExtraBold, true);
    }

    void Card(RectTransform grid, int cx, int cy, GameObject model, Color stripe, Color titleColor, string title, string body, bool reels = false)
    {
        const float W = 370f, H = 226f;
        var card = UIKit.ArtImage(grid, "card", new Vector2(0.5f, 0.5f), new Vector2((cx - 1) * (W + 22f), (0.5f - cy) * (H + 26f)), H, W);
        card.name = "Card_" + title;
        var cr = card.rectTransform;
        // Coloured lip along the top edge.
        var lip = Hud.NewImage("Lip", cr, UISprites.RoundedRect, stripe);
        lip.pixelsPerUnitMultiplier = 6f;
        Hud.Anchor(lip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(W - 34f, 6f));

        const float iconX = 72f;
        if (reels) MiniReels(cr, new Vector2(iconX, -6f));
        else if (model != null)
        {
            var img = ModelViewport.Create(cr, model, new Vector2(124f, 124f), 35f, 30f, 16f, 1.05f);
            Hud.Anchor(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(iconX, -6f), new Vector2(124f, 124f));
        }

        float textLeft = 136f, textW = W - textLeft - 22f;
        var t = UIKit.Label(cr, title, title.Length > 14 ? 22f : 26f, titleColor, UIWeight.ExtraBold);
        Hud.Anchor(t.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft + textW * 0.5f, -42f), new Vector2(textW + 30f, 34f));

        body = body.Replace("<b>", "<color=#FFC93A><font=\"Poppins-Bold SDF\">").Replace("</b>", "</font></color>");
        var b = UIKit.Label(cr, body, 15.5f, new Color(0.93f, 0.92f, 0.88f), UIWeight.Medium);
        b.richText = true;
        b.alignment = TextAlignmentOptions.TopLeft;
        b.textWrappingMode = TextWrappingModes.Normal;
        b.lineSpacing = 2f;
        Hud.Anchor(b.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft + textW * 0.5f, -64f - 64f), new Vector2(textW + 6f, 132f));
    }

    /// <summary>Two little gold slot-reel digits showing 07, like the number on the HUD.</summary>
    void MiniReels(RectTransform parent, Vector2 pos)
    {
        for (int i = 0; i < 2; i++)
        {
            var w = Hud.NewImage("Reel", parent, UISprites.RoundedRect, new Color(1f, 0.8f, 0.22f));
            w.pixelsPerUnitMultiplier = 1.6f;
            Hud.Anchor(w.rectTransform, new Vector2(0f, 0.5f), pos + new Vector2(-24f + i * 48f, 0f), new Vector2(44f, 60f));
            var d = UIKit.Label(w.rectTransform, i == 0 ? "0" : "7", 46f, new Color(0.16f, 0.12f, 0.08f), UIWeight.ExtraBold);
            Hud.Stretch(d.rectTransform);
            d.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        }
    }

    // ---------------- pause ----------------

    void BuildPause()
    {
        pauseGroup = NewPage("Pause", new Color(0.02f, 0.03f, 0.05f, 0.35f));
        var r = (RectTransform)pauseGroup.transform;
        var board = UIKit.ArtImage(r, "board", new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), 484f);
        board.preserveAspect = true;
        var c = board.rectTransform;

        // The title sits on the board's steel nameplate.
        var head = UIKit.Label(c, "PAUSED", 56f, Color.white, UIWeight.ExtraBold, true);
        UIKit.GoldGradient(head);
        Hud.Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(300f, 70f));

        UIKit.PaintedButton(c, "btn_yellow", "RESUME", InkYellow, new Vector2(0.5f, 0.5f), new Vector2(0f, 98f), new Vector2(300f, 70f), 36f,
                            () => Pause(false));
        UIKit.PaintedButton(c, "btn_teal", "RESTART", InkTeal, new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(262f, 50f), 26f, () =>
        {
            Pause(false);
            if (MatchManager.Instance != null) MatchManager.Instance.PlayAgain();
        });
        UIKit.PaintedButton(c, "btn_slate", "HOW TO PLAY", InkSlate, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(262f, 50f), 26f,
                            () => OpenHowTo(Page.Pause));
        UIKit.PaintedButton(c, "btn_cream", "MAIN MENU", InkCream, new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(262f, 50f), 26f, () =>
        {
            Pause(false);
            if (MatchManager.Instance != null) MatchManager.Instance.BackToMenu();
        });
        AddVolumeSliders(c, new Vector2(4f, -140f), new Vector2(4f, -178f), 82f, 186f, 22f);
    }

    // ---------------- widgets ----------------

    void AddVolumeSliders(RectTransform parent, Vector2 musicPos, Vector2 soundPos, float labelW, float trackW, float h)
    {
        float music = GameAudio.I != null ? GameAudio.I.MusicVolume : 0.8f;
        float sfx = GameAudio.I != null ? GameAudio.I.SfxVolume : 0.9f;
        musicSliders.Add(UIKit.PaintedSlider(parent, "MUSIC", new Vector2(0.5f, 0.5f), musicPos, labelW, trackW, music, v =>
        {
            if (GameAudio.I != null) GameAudio.I.MusicVolume = v;
            SyncSliders();
        }, h));
        sfxSliders.Add(UIKit.PaintedSlider(parent, "SOUND", new Vector2(0.5f, 0.5f), soundPos, labelW, trackW, sfx, v =>
        {
            if (GameAudio.I != null) { GameAudio.I.SfxVolume = v; GameAudio.Hover(); }
            SyncSliders();
        }, h));
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
