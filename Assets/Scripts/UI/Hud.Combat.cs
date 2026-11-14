using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Combat half of the HUD: player health + blaster bars, chunky bars floating over every car,
/// pop-up damage numbers, Turbo cooldown icon, countdown, match timer,
/// kill feed, hit vignette, BUSTED popup and the results screen.
/// </summary>
public partial class Hud
{
    static readonly Color HpGreen = new Color(0.35f, 0.92f, 0.38f);
    static readonly Color HpYellow = new Color(1f, 0.82f, 0.2f);
    static readonly Color HpRed = new Color(1f, 0.3f, 0.25f);
    static readonly Color TealBar = new Color(0.3f, 0.95f, 0.85f);
    static readonly Color GoldBar = new Color(1f, 0.8f, 0.2f);
    static readonly Color BarBack = new Color(0.07f, 0.08f, 0.1f, 0.9f);

    // Player panel
    RectTransform hpFill, blasterFill;
    Image hpFillImg, blasterFillImg;
    TextMeshProUGUI hpText, blasterText, nameText;
    GameObject blasterRow;
    CarHealth pHealth;
    TurboAbility pTurbo;

    // Abilities
    Image turboFill, turboBack, turboTip, turboTipGlow, turboHub, turboHubGlow, turboSpill;
    TextMeshProUGUI turboLabel;
    float turboRadius;
    TextMeshProUGUI turboKey;
    Image stormBanner;
    static readonly Color StormInk = new Color(0.2f, 0.12f, 0.06f);
    static readonly Color TurboOrange = new Color(1f, 0.55f, 0.15f);
    static readonly Color TurboBlue = new Color(0.3f, 0.85f, 1f);
    static readonly Color StormDust = new Color(0.24f, 0.16f, 0.05f);

    // Floating bar over each car: one bar, only while it's being hit, showing its outermost layer
    // (Jackpot cannon bar -> small blaster bar -> car health).
    const float BarShowTime = 1.5f;   // fully visible this long after the last hit...
    const float BarFadeTime = 0.8f;   // ...then fades out over this long
    class WorldBar
    {
        public RectTransform root, fill;
        public Image fillImg;
        public CanvasGroup group;
        public TextMeshProUGUI label;
        public SpriteText name;
        public bool isPlayer;
        public CarHealth health;
        public CarLoadout loadout;
        public float sinceHit = 99f;
        public float pop;
        public BlasterTier shownLayer;
    }
    readonly Dictionary<CarHealth, WorldBar> worldBars = new Dictionary<CarHealth, WorldBar>();

    // Small odometer number floating over each AI car (gold when locked).
    // Over each AI car: its colour number plate (Docs/NPC_Naming art) with its live number in painted digits.
    class FloatingNumber { public RectTransform root; public Image back; public SpriteText digits; public Color tint; public Odometer odo; public CarHealth health; public DiceLuck luck; public Image[] pips; }
    const float PlateHeight = 36f, PlateLift = 36f;      // plate size and how far above the car's roof point
    static readonly Vector2 BarFrameSize = new Vector2(108f, 25f);
    readonly List<FloatingNumber> floatingNumbers = new List<FloatingNumber>();
    readonly HashSet<CarIdentity> floatingFor = new HashSet<CarIdentity>();
    RectTransform worldBarLayer, damageLayer;

    // Damage numbers
    class DamageNumber { public TextMeshProUGUI text; public Vector3 world; public float age; public float xDrift; }
    readonly List<DamageNumber> damageNumbers = new List<DamageNumber>();
    readonly Stack<DamageNumber> damagePool = new Stack<DamageNumber>();

    // Storm
    TextMeshProUGUI stormText, stormWarning;
    Image stormOverlay;
    float stormOverlayAlpha;

    // Top bar, feed, overlays, results
    TextMeshProUGUI timerText, carsLeftText;
    RectTransform feedLayer;
    readonly List<(TextMeshProUGUI text, float age)> feed = new List<(TextMeshProUGUI, float)>();
    Image vignetteImg, flashImg;
    float vignette, flashAlpha;
    GameObject resultsPanel;
    TextMeshProUGUI resultsTitle, resultsSub;
    RectTransform resultsCard;
    float resultsAge;
    RectTransform resultsButtons;
    RawImage resultsTrophy;
    TextMeshProUGUI resultsLabel;
    Image resultsRays;
    bool matchBound;

    // ---------------- build ----------------

    void BuildCombatUI()
    {
        worldBarLayer = NewRect("WorldBars", canvasRect, Vector2.zero, Vector2.zero);
        Stretch(worldBarLayer);
        worldBarLayer.SetSiblingIndex(1);
        damageLayer = NewRect("DamageNumbers", canvasRect, Vector2.zero, Vector2.zero);
        Stretch(damageLayer);
        damageLayer.SetSiblingIndex(2);

        vignetteImg = NewImage("HitVignette", canvasRect, UISprites.Vignette, new Color(1f, 0.1f, 0.1f, 0f));
        Stretch(vignetteImg.rectTransform);
        vignetteImg.rectTransform.SetSiblingIndex(0);
        flashImg = NewImage("ScreenFlash", canvasRect, null, new Color(1f, 0.9f, 0.5f, 0f));
        Stretch(flashImg.rectTransform);

        // Health, bottom-left: the painted metal frame; the green bar runs in its slot.
        var frame = UIKit.ArtImage(canvasRect, "hp_frame", new Vector2(0f, 0f), new Vector2(176f, 44f), 61f, 330f);
        var fr = frame.rectTransform;
        nameText = NewText("Name", fr, "", 1, Color.clear);
        nameText.gameObject.SetActive(false);
        blasterRow = NewRect("BlasterRow", fr, Vector2.zero, Vector2.zero).gameObject;
        var br = (RectTransform)blasterRow.transform;
        Anchor(br, new Vector2(0f, 1f), new Vector2(150f, 13f), new Vector2(300f, 24f));
        blasterText = UIKit.Label(br, "", 15f, Color.white, UIWeight.ExtraBold, true);
        blasterText.alignment = TextAlignmentOptions.Left;
        Hud.Stretch(blasterText.rectTransform);
        MakeBar(br, new Vector2(54f, 0f), new Vector2(196f, 16f), GoldBar, out blasterFill, out blasterFillImg);

        var hpLabel = UIKit.Label(fr, "HP", 25f, UIKit.Cream, UIWeight.ExtraBold, true);
        Anchor(hpLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-126f, 0f), new Vector2(50f, 34f));
        var slot = NewRect("HpSlot", fr, Vector2.zero, Vector2.zero);
        Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(-5.6f, -2.4f), new Vector2(164f, 12f));
        hpFillImg = NewImage("Fill", slot, UISprites.RoundedRect, HpGreen);
        hpFillImg.pixelsPerUnitMultiplier = 3f;
        hpFill = hpFillImg.rectTransform;
        hpFill.anchorMin = Vector2.zero; hpFill.anchorMax = Vector2.one;
        hpFill.pivot = new Vector2(0f, 0.5f);
        hpFill.offsetMin = hpFill.offsetMax = Vector2.zero;
        var shine = NewImage("Shine", hpFill, null, new Color(1f, 1f, 1f, 0.28f));
        shine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
        shine.rectTransform.anchorMax = new Vector2(1f, 0.85f);
        shine.rectTransform.offsetMin = new Vector2(3f, 0f);
        shine.rectTransform.offsetMax = new Vector2(-3f, 0f);
        hpText = UIKit.Label(fr, "100", 25f, UIKit.Cream, UIWeight.ExtraBold, true);
        Anchor(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(122f, 0f), new Vector2(64f, 34f));

        // Turbo, bottom-right: the painted gauge; its dark ring fills as Turbo recharges.
        BuildTurboGauge(new Vector2(-80f, 74f), 142f);

        // Top: the timer screen with cars left, and the storm warning banner under it.
        var timerFrame = UIKit.ArtImage(canvasRect, "timer_screen", new Vector2(0.5f, 1f), new Vector2(0f, -46f), 82f, 168f);
        timerText = UIKit.Label(timerFrame.rectTransform, "0:00", 38f, UIKit.Cream, UIWeight.ExtraBold, true);
        Anchor(timerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 9f), new Vector2(160f, 46f));
        carsLeftText = UIKit.Label(timerFrame.rectTransform, "", 14f, UIKit.Cream, UIWeight.Bold, true);
        Anchor(carsLeftText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(160f, 20f));

        stormBanner = UIKit.ArtImage(canvasRect, "storm_banner", new Vector2(0.5f, 1f), new Vector2(0f, -112f), 38f, 290f);
        stormText = UIKit.Label(stormBanner.rectTransform, "", 15f, StormInk, UIWeight.ExtraBold);
        Anchor(stormText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(17f, 1f), new Vector2(240f, 30f));
        stormOverlay = NewImage("StormOverlay", canvasRect, UISprites.Vignette, new Color(StormDust.r, StormDust.g, StormDust.b, 0f));
        Stretch(stormOverlay.rectTransform);
        stormOverlay.rectTransform.SetSiblingIndex(0);
        stormWarning = NewText("StormWarning", canvasRect, "GET OUT OF THE STORM!", 34, new Color(1f, 0.78f, 0.35f));
        stormWarning.fontStyle = FontStyles.Bold;
        Anchor(stormWarning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(800f, 44f));
        Outline(stormWarning);
        stormWarning.gameObject.SetActive(false);

        feedLayer = NewRect("KillFeed", canvasRect, Vector2.zero, new Vector2(420f, 200f));
        feedLayer.anchorMin = feedLayer.anchorMax = new Vector2(1f, 1f);
        feedLayer.pivot = new Vector2(1f, 1f);
        feedLayer.anchoredPosition = new Vector2(-16f, -14f);

        BuildResults();
    }

    void MakeBar(RectTransform parent, Vector2 pos, Vector2 size, Color color, out RectTransform fill, out Image fillImg)
    {
        var back = NewImage("BarBack", parent, UISprites.RoundedRect, BarBack);
        Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
        fillImg = NewImage("Fill", back.rectTransform, UISprites.RoundedRect, color);
        fill = fillImg.rectTransform;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        var shine = NewImage("Shine", fill, null, new Color(1f, 1f, 1f, 0.22f));
        shine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
        shine.rectTransform.anchorMax = new Vector2(1f, 0.9f);
        shine.rectTransform.offsetMin = new Vector2(3f, 0f);
        shine.rectTransform.offsetMax = new Vector2(-3f, 0f);
    }

    static void SetFill(RectTransform fill, float fraction)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        fill.gameObject.SetActive(fraction > 0.005f);
    }

    /// <summary>
    /// The painted Turbo gauge. A thick ring overlays the dial's dark track: radial fill over the
    /// 270 degrees from bottom-left round the top to bottom-right (the SHIFT plate sits in the gap).
    /// </summary>
    void BuildTurboGauge(Vector2 pos, float size)
    {
        turboBack = UIKit.ArtImage(canvasRect, "turbo_dial", new Vector2(1f, 0f), pos, size);
        turboBack.preserveAspect = true;
        var dial = turboBack.rectTransform;
        turboRadius = size * 0.5f;

        // Light in the dial's channel: a soft spill of light first, then the painted gradient arc
        // (a little see-through so the dial's tick marks still show), filling from bottom-left round the top.
        turboSpill = NewImage("TurboArcSpill", dial, UISprites.TurboArcGlow, new Color(1f, 1f, 1f, 0.5f));
        Anchor(turboSpill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.01f), Vector2.one * size * 0.9f);
        turboSpill.type = Image.Type.Filled;
        turboSpill.fillMethod = Image.FillMethod.Radial360;
        turboSpill.fillOrigin = (int)Image.Origin360.Bottom;
        turboSpill.fillClockwise = true;
        turboSpill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        turboFill = NewImage("TurboArc", dial, UISprites.TurboArc, Color.white);
        Anchor(turboFill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.01f), Vector2.one * size * 0.857f);
        turboFill.type = Image.Type.Filled;
        turboFill.fillMethod = Image.FillMethod.Radial360;
        turboFill.fillOrigin = (int)Image.Origin360.Bottom;
        turboFill.fillClockwise = true;
        turboFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

        // Hot spot at the leading edge of the light: a soft glow with a bright streak in it.
        turboTipGlow = NewImage("TurboTipGlow", dial, UISprites.Glow, new Color(1f, 0.85f, 0.4f, 0.6f));
        turboTipGlow.rectTransform.anchorMin = turboTipGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        turboTipGlow.rectTransform.sizeDelta = Vector2.one * size * 0.36f;
        turboTip = NewImage("TurboTip", dial, UISprites.Glow, new Color(1f, 0.97f, 0.75f, 0.95f));
        turboTip.rectTransform.anchorMin = turboTip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        turboTip.rectTransform.sizeDelta = new Vector2(size * 0.08f, size * 0.3f);

        // Cyan ring of light round the hub, and the TURBO lettering glowing inside it.
        turboHubGlow = NewImage("TurboHubGlow", dial, UISprites.Glow, new Color(TurboBlue.r, TurboBlue.g, TurboBlue.b, 0.3f));
        Anchor(turboHubGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.02f), Vector2.one * size * 0.62f);
        // One solid, still blue ring (a spinning comet ring confused players).
        turboHub = NewImage("TurboHubRing", dial, UISprites.Ring, TurboBlue);
        Anchor(turboHub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.02f), Vector2.one * size * 0.5f);
        turboLabel = UIKit.Label(dial, "TURBO", size * 0.1f, new Color(0.82f, 1f, 1f), UIWeight.SemiBold);
        turboLabel.characterSpacing = 2f;
        Anchor(turboLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.02f), new Vector2(size * 0.6f, size * 0.2f));
    }

    void MakeAbility(Vector2 pos, string icon, string key, Color color, out Image back, out Image fill, out TextMeshProUGUI keyText, out TextMeshProUGUI iconText)
    {
        back = NewImage("Ability_" + icon, canvasRect, UISprites.Circle, BarBack);
        Anchor(back.rectTransform, new Vector2(1f, 0f), pos, new Vector2(76f, 76f));
        fill = NewImage("Fill", back.rectTransform, UISprites.Circle, color);
        Anchor(fill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 66f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        var inner = NewImage("Inner", back.rectTransform, UISprites.Circle, new Color(0.1f, 0.12f, 0.14f, 1f));
        Anchor(inner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
        iconText = NewText("Icon", back.rectTransform, icon, icon.Length > 3 ? 14 : 20, color);
        iconText.fontStyle = FontStyles.Bold;
        Anchor(iconText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 30f));
        keyText = NewText("Key", back.rectTransform, key, 13, Color.white);
        Anchor(keyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(90f, 18f));
        Outline(keyText);
    }

    void BuildResults()
    {
        var dim = NewImage("Results", canvasRect, null, new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        resultsPanel = dim.gameObject;

        var card = UIKit.ArtImage(dim.rectTransform, "card", new Vector2(0.5f, 0.5f), Vector2.zero, 350f, 640f);
        resultsCard = card.rectTransform;
        // "YOU PLACED" on top, the place itself big in the middle, then a small line about the match.
        resultsLabel = UIKit.Label(resultsCard, "YOU PLACED", 28f, UIKit.Cream, UIWeight.ExtraBold, true);
        resultsLabel.characterSpacing = 3f;
        Anchor(resultsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(560f, 40f));
        resultsTitle = UIKit.Label(resultsCard, "1ST", 100f, GoldBar, UIWeight.ExtraBold, true);
        Anchor(resultsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(560f, 110f));
        resultsSub = UIKit.Label(resultsCard, "", 18f, Color.white, UIWeight.SemiBold, true);
        resultsSub.richText = true;
        resultsSub.textWrappingMode = TextWrappingModes.Normal;
        Anchor(resultsSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(560f, 48f));

        // Both buttons the same size, side by side.
        resultsButtons = NewRect("Buttons", resultsCard, Vector2.zero, new Vector2(600f, 80f));
        Anchor(resultsButtons, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(600f, 80f));

        resultsPanel.SetActive(false);

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---------------- binding ----------------

    void OnEnable()
    {
        CarHealth.AnyDamaged += OnAnyDamaged;
        Pickup.AnyCollected += OnPickup;
        BlackCat.AnyScrambled += OnCat;
    }

    void OnDisable()
    {
        CarHealth.AnyDamaged -= OnAnyDamaged;
        Pickup.AnyCollected -= OnPickup;
        BlackCat.AnyScrambled -= OnCat;
        if (Instance == this) Instance = null;
    }

    void OnPickup(Pickup p, GameObject car, string message)
    {
        if (player == null || car != player.gameObject) return;
        bool clover = p.Kind == PickupKind.Clover;
        int bang = message.IndexOf('!');
        ShowPopup(clover ? "LUCKY CLOVER!" : message.Substring(0, bang + 1), clover ? HpGreen : Color.white, message.Substring(bang + 1).Trim());
    }

    void OnCat(GameObject car, int newNumber)
    {
        if (player == null || car != player.gameObject) return;
        ShowPopup("BLACK CAT!", new Color(0.75f, 0.5f, 1f), "bad luck - number scrambled to " + newNumber + (shownStacks > 0 ? ", dice luck gone" : ""));
        CameraShake.Add(0.2f);
    }

    void BindPlayerCombat()
    {
        pHealth = player.GetComponent<CarHealth>();
        pTurbo = player.GetComponent<TurboAbility>();
        nameText.text = player.displayName.ToUpper();
        if (pHealth != null) pHealth.Damaged += _ => vignette = 0.55f;
        if (pTurbo != null) pTurbo.Activated += () => CameraShake.Add(0.15f);
    }

    void BindMatch()
    {
        if (matchBound || MatchManager.Instance == null) return;
        matchBound = true;
        var mm = MatchManager.Instance;
        mm.CountdownTick += n =>
        {
            if (n > 0) ShowPopup(n.ToString(), Color.white, "");
            else ShowPopup("GO!", HpGreen, "last car standing wins");
        };
        mm.CarEliminated += OnCarEliminated;
        mm.ResultsShown += ShowResults;
    }

    void OnAnyDamaged(DamageInfo d)
    {
        if (d.victim != null && worldBars.TryGetValue(d.victim, out var bar)) { bar.sinceHit = 0f; bar.pop = 1f; }
        if (d.amount < 0.5f) return;
        bool victimIsPlayer = d.victim != null && d.victim.Identity != null && d.victim.Identity.isPlayer;
        bool attackerIsPlayer = d.attacker != null && d.attacker.isPlayer;
        Color c = victimIsPlayer ? HpRed : Color.white;
        if (d.kind == DamageKind.Ram || d.kind == DamageKind.Blast) c = victimIsPlayer ? HpRed : GoldBar;
        float size = d.kind == DamageKind.Ram || d.kind == DamageKind.Blast ? 40f : 30f;
        SpawnDamageNumber(d.point + Vector3.up * 1.5f, Mathf.CeilToInt(d.amount).ToString(), c, size);
        if (d.kind == DamageKind.Storm) c = new Color(1f, 0.75f, 0.3f);
        if (victimIsPlayer && d.kind != DamageKind.Storm) CameraShake.Add(Mathf.Clamp(0.18f + d.amount / 50f, 0f, 0.6f));
        else if (attackerIsPlayer) CameraShake.Add(d.kind == DamageKind.Blaster ? 0.05f : 0.25f);
    }

    void OnCarEliminated(CarHealth victim, CarIdentity killer, int placement)
    {
        string victimName = victim.Identity != null ? victim.Identity.displayName : "?";
        Color vc = victim.Identity != null ? victim.Identity.color : Color.white;
        string line = killer != null
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(killer.color)}>{killer.displayName}</color> busted <color=#{ColorUtility.ToHtmlStringRGB(vc)}>{victimName}</color>"
            : $"<color=#{ColorUtility.ToHtmlStringRGB(vc)}>{victimName}</color> wrecked";
        AddFeed(line);
        if (victim.Identity != null && victim.Identity.isPlayer)
            ShowPopup("WRECKED!", HpRed, "you finished " + MatchManager.Ordinal(placement));
        else if (killer != null && killer.isPlayer)
            ShowPopup("TAKEDOWN!", GoldBar, victimName + " is out");
        CameraShake.Add(0.3f);
    }

    void AddFeed(string richText)
    {
        var t = NewText("Feed", feedLayer, richText, 18, Color.white);
        t.alignment = TextAlignmentOptions.Right;
        t.richText = true;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.sizeDelta = new Vector2(420f, 24f);
        Outline(t);
        feed.Insert(0, (t, 0f));
        while (feed.Count > 5) { Destroy(feed[feed.Count - 1].text.gameObject); feed.RemoveAt(feed.Count - 1); }
    }

    // ---------------- per frame ----------------

    void UpdateCombatUI()
    {
        if (!matchBound) BindMatch();
        float dt = Time.deltaTime;

        UpdatePlayerPanel();
        UpdateAbilities();
        UpdateWorldBars();
        UpdateDamageNumbers(dt);

        var mm = MatchManager.Instance;
        if (mm != null)
        {
            int secs = Mathf.FloorToInt(mm.MatchTime);
            timerText.text = $"{secs / 60}:{secs % 60:00}";
            carsLeftText.text = mm.State == MatchState.Countdown ? "GET READY" : $"CARS LEFT: {mm.CarsLeft}";
            if (mm.ResultsVisible)
            {
                resultsAge += Time.unscaledDeltaTime;
                float s = resultsAge < 0.2f ? Mathf.Lerp(0.6f, 1.08f, resultsAge / 0.2f) : 1f + 0.08f * Mathf.Exp(-(resultsAge - 0.2f) * 8f) * Mathf.Cos((resultsAge - 0.2f) * 22f);
                resultsCard.localScale = Vector3.one * s;
                if (resultsRays != null) resultsRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -resultsAge * 18f);
                var k = Keyboard.current;
                if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) mm.PlayAgain();
            }
        }

        for (int i = feed.Count - 1; i >= 0; i--)
        {
            var (text, age) = feed[i];
            age += dt;
            feed[i] = (text, age);
            text.rectTransform.anchoredPosition = Vector2.Lerp(text.rectTransform.anchoredPosition, new Vector2(0f, -i * 26f), 1f - Mathf.Exp(-12f * dt));
            text.alpha = Mathf.Clamp01(5f - age);
            if (age > 5f) { Destroy(text.gameObject); feed.RemoveAt(i); }
        }

        UpdateStormUI(dt);
        vignette = Mathf.Max(0f, vignette - dt * 1.6f);
        vignetteImg.color = new Color(1f, 0.1f, 0.1f, vignette);
        flashAlpha = Mathf.Max(0f, flashAlpha - dt * 1.8f);
        flashImg.color = new Color(1f, 0.9f, 0.5f, flashAlpha);
    }

    void UpdateStormUI(float dt)
    {
        var storm = StormController.Instance;
        if (storm == null) { stormText.text = ""; stormBanner.gameObject.SetActive(false); return; }
        stormBanner.gameObject.SetActive(true);
        stormText.color = storm.IsShrinking ? new Color(0.55f, 0.07f, 0.05f) : StormInk;
        if (storm.IsShrinking) stormText.text = "STORM CLOSING IN!";
        else if (storm.TimeToNextShrink > 0f)
        {
            int s = Mathf.CeilToInt(storm.TimeToNextShrink);
            stormText.text = $"STORM SHRINKS IN {s / 60}:{s % 60:00}";
        }
        else stormText.text = "FINAL STORM ZONE";
        stormText.alpha = storm.IsShrinking ? 0.7f + 0.3f * Mathf.Sin(Time.time * 8f) : 1f;
        stormBanner.rectTransform.localScale = Vector3.one * (storm.IsShrinking ? 1f + 0.03f * Mathf.Sin(Time.time * 8f) : 1f);

        bool outside = pHealth != null && pHealth.IsAlive && !storm.IsInside(player.transform.position, 0f);
        // A soft dark vignette; the StormVolume (faded by StormController) does the desaturation.
        stormOverlayAlpha = Mathf.MoveTowards(stormOverlayAlpha, outside ? 0.45f : 0f, dt * 1.2f);
        stormOverlay.color = new Color(StormDust.r, StormDust.g, StormDust.b, stormOverlayAlpha);
        stormWarning.gameObject.SetActive(outside);
        if (outside) stormWarning.alpha = 0.6f + 0.4f * Mathf.Sin(Time.time * 7f);
    }

    void UpdatePlayerPanel()
    {
        if (pHealth == null) return;
        float hp = pHealth.Health / pHealth.MaxHealth;
        SetFill(hpFill, hp);
        hpFillImg.color = HpColor(hp);
        if (pHealth.IsRegenerating) hpFillImg.color = Color.Lerp(hpFillImg.color, Color.white, 0.25f + 0.2f * Mathf.Sin(Time.time * 6f));
        hpText.text = (pHealth.IsRegenerating ? "<color=#7CFF8A>+</color>" : "") + Mathf.FloorToInt(pHealth.Health);

        // Which blaster you carry (no separate bar: it's yours until the car is destroyed).
        bool armed = loadout != null && loadout.IsArmed;
        bool jackpot = armed && loadout.Tier == BlasterTier.Jackpot;
        blasterFill.parent.gameObject.SetActive(false);
        blasterText.text = !armed ? "NO BLASTER - find a chest!" : jackpot ? "JACKPOT CANNON" : "SMALL BLASTER";
        blasterText.color = !armed ? new Color(0.75f, 0.75f, 0.75f) : jackpot ? GoldBar : TealBar;
    }

    void UpdateAbilities()
    {
        if (pTurbo != null)
        {
            float dt = Time.deltaTime, t = Time.time;
            bool active = pTurbo.IsActive, ready = pTurbo.IsReady;
            // 0.75 = the full 270-degree arc.
            float fill = 0.75f * (active ? pTurbo.ActiveLeft / 2f : pTurbo.Readiness);
            turboFill.fillAmount = fill;
            float pulse = ready ? 1f + 0.035f * Mathf.Sin(t * 5f) : 1f;
            turboBack.rectTransform.localScale = Vector3.one * (active ? 1.08f : pulse);

            // The light: dimmer while recharging, glowing and breathing when ready, flaring while active.
            float glow = active ? 0.9f + 0.1f * Mathf.Sin(t * 30f) : ready ? 0.92f + 0.08f * Mathf.Sin(t * 5f) : 0.86f;
            turboFill.color = new Color(1f, glow, glow, 1f);
            turboSpill.fillAmount = fill;
            turboSpill.color = new Color(1f, 1f, 1f, active ? 0.75f + 0.2f * Mathf.Sin(t * 30f) : ready ? 0.5f + 0.15f * Mathf.Sin(t * 5f) : 0.4f);

            // Streak at the leading edge, while the arc is part-full.
            bool showTip = fill > 0.01f && fill < 0.749f;
            turboTip.enabled = turboTipGlow.enabled = showTip;
            if (showTip)
            {
                float a = (45f + fill * 360f) * Mathf.Deg2Rad;              // clockwise from the bottom
                Vector2 dir = new Vector2(-Mathf.Sin(a), -Mathf.Cos(a));
                Vector2 p = dir * turboRadius * 0.69f + new Vector2(0f, turboRadius * 0.02f);
                turboTip.rectTransform.anchoredPosition = p;
                turboTip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                turboTip.color = new Color(1f, 0.97f, 0.75f, 0.8f + 0.2f * Mathf.Sin(t * 12f));
                turboTipGlow.rectTransform.anchoredPosition = p - dir * turboRadius * 0.04f;
                turboTipGlow.color = new Color(1f, 0.85f, 0.4f, 0.5f + 0.15f * Mathf.Sin(t * 9f));
            }

            // The blue hub ring stays one solid, still colour (set once when built).
        }
    }

    void UpdateWorldBars()
    {
        if (cam == null) return;
        foreach (var id in CarIdentity.All)
        {
            var h = id.GetComponent<CarHealth>();
            if (h != null && !worldBars.ContainsKey(h)) worldBars[h] = MakeWorldBar(h, id);
            if (!id.isPlayer && h != null && floatingFor.Add(id)) floatingNumbers.Add(MakeFloatingNumber(id, h));
        }
        Vector2 size = canvasRect.rect.size;
        float dt = Time.deltaTime;

        foreach (var fnum in floatingNumbers)
        {
            bool alive = fnum.health != null && fnum.health.IsAlive && fnum.odo != null;
            Vector3 vp = alive ? cam.WorldToViewportPoint(fnum.health.transform.position + Vector3.up * 4.4f) : Vector3.back;
            bool show = alive && vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
            fnum.root.gameObject.SetActive(show);
            if (!show) continue;
            fnum.root.anchoredPosition = new Vector2(vp.x * size.x, vp.y * size.y + PlateLift);
            bool locked = fnum.odo.IsLocked;
            fnum.digits.SetText(fnum.odo.Number.ToString());
            fnum.digits.Color = locked ? new Color(1f, 0.84f, 0.32f) : fnum.tint;
            int stacks = fnum.luck != null ? fnum.luck.Stacks : 0;
            for (int i = 0; i < fnum.pips.Length; i++) fnum.pips[i].enabled = i < stacks;
        }
        foreach (var kv in worldBars)
        {
            var b = kv.Value;
            b.sinceHit += dt;
            float alpha = b.sinceHit < BarShowTime ? 1f : 1f - (b.sinceHit - BarShowTime) / BarFadeTime;
            bool alive = b.health != null && b.health.IsAlive;
            if (!alive || alpha <= 0f) { b.root.gameObject.SetActive(false); continue; }

            Vector3 vp = cam.WorldToViewportPoint(b.health.transform.position + Vector3.up * 4.4f);
            bool visible = vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
            b.root.gameObject.SetActive(visible);
            if (!visible) continue;
            // AI: above the number plate. Player (no plate): just over the roof.
            b.root.anchoredPosition = new Vector2(vp.x * size.x, vp.y * size.y + (b.isPlayer ? 30f : PlateLift + PlateHeight * 0.5f + 22f));
            b.group.alpha = alpha;

            float hp = b.health.Health / b.health.MaxHealth;
            SetFill(b.fill, hp);
            b.fillImg.color = HpColor(hp);

            b.pop = Mathf.Max(0f, b.pop - dt * 6f);
            b.root.localScale = Vector3.one * (1f + 0.12f * b.pop);
        }
    }

    FloatingNumber MakeFloatingNumber(CarIdentity id, CarHealth h)
    {
        var f = new FloatingNumber { health = h, odo = id.GetComponent<Odometer>(), luck = DiceLuck.For(id.gameObject) };
        var plate = UIKit.Art(PlateFor(id));
        float plateW = plate != null ? PlateHeight * plate.rect.width / plate.rect.height : PlateHeight * 1.6f;
        f.root = NewRect("Number_" + id.displayName, worldBarLayer, Vector2.zero, new Vector2(plateW, PlateHeight));
        f.root.anchorMin = f.root.anchorMax = Vector2.zero;
        f.back = NewImage("Plate", f.root, plate, Color.white);
        Stretch(f.back.rectTransform);
        // Pale digits tinted toward the car's colour, as on the painted example plates.
        f.tint = Color.Lerp(id.color, new Color(0.98f, 0.95f, 0.86f), 0.68f);
        f.digits = SpriteText.Create(f.root, "Digits", PlateHeight * 0.5f, f.tint, new Vector2(0f, PlateHeight * 0.08f));
        // Little gold dots under the number: how much dice luck this car is holding.
        f.pips = new Image[DiceLuck.MaxStacks];
        for (int i = 0; i < f.pips.Length; i++)
        {
            f.pips[i] = NewImage("LuckDot" + i, f.root, UISprites.Circle, GoldBar);
            Anchor(f.pips[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(-16f + i * 8f, -5f), new Vector2(6f, 6f));
        }
        f.root.gameObject.SetActive(false);
        return f;
    }

    WorldBar MakeWorldBar(CarHealth h, CarIdentity id)
    {
        var b = new WorldBar { health = h, loadout = h.GetComponent<CarLoadout>() };
        b.root = NewRect("Bar_" + id.displayName, worldBarLayer, Vector2.zero, new Vector2(10f, 10f));
        b.root.anchorMin = b.root.anchorMax = Vector2.zero;
        b.group = b.root.gameObject.AddComponent<CanvasGroup>();
        b.group.blocksRaycasts = false;
        b.group.interactable = false;
        b.shownLayer = b.loadout != null ? b.loadout.Tier : BlasterTier.None;
        b.isPlayer = id.isPlayer;
        // Name in the painted letters, in the car's colour.
        b.name = SpriteText.Create(b.root, "Name", 15f, Color.Lerp(id.color, Color.white, 0.12f), new Vector2(0f, BarFrameSize.y * 0.5f + 11f));
        b.name.SetText(id.isPlayer ? "YOU" : id.displayName);
        // The rusty frame; the health fill runs in its dark slot.
        var frame = UIKit.ArtImage(b.root, "npc_bar_frame", new Vector2(0.5f, 0.5f), Vector2.zero, BarFrameSize.y, BarFrameSize.x);
        var slot = NewRect("Slot", frame.rectTransform, Vector2.zero, Vector2.zero);
        Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.4f), new Vector2(BarFrameSize.x * 0.86f, BarFrameSize.y * 0.42f));
        b.fillImg = NewImage("Fill", slot, UISprites.RoundedRect, HpGreen);
        b.fillImg.pixelsPerUnitMultiplier = 4f;
        b.fill = b.fillImg.rectTransform;
        b.fill.anchorMin = Vector2.zero; b.fill.anchorMax = Vector2.one;
        b.fill.pivot = new Vector2(0f, 0.5f);
        b.fill.offsetMin = b.fill.offsetMax = Vector2.zero;
        var gloss = NewImage("Gloss", b.fill, null, new Color(1f, 1f, 1f, 0.3f));
        gloss.rectTransform.anchorMin = new Vector2(0f, 0.55f);
        gloss.rectTransform.anchorMax = new Vector2(1f, 0.88f);
        gloss.rectTransform.offsetMin = new Vector2(3f, 0f);
        gloss.rectTransform.offsetMax = new Vector2(-3f, 0f);
        b.root.gameObject.SetActive(false);
        return b;
    }

    /// <summary>Which painted number plate a car carries (by its colour scheme).</summary>
    static string PlateFor(CarIdentity id)
    {
        string n = id.displayName.ToLowerInvariant();
        if (n.Contains("cat")) return "npc_plate_purple";
        if (n.Contains("dice")) return "npc_plate_orange";
        if (n.Contains("8")) return "npc_plate_red";
        return "npc_plate_green";
    }

    static Color HpColor(float f) => f > 0.5f ? Color.Lerp(HpYellow, HpGreen, (f - 0.5f) * 2f) : Color.Lerp(HpRed, HpYellow, f * 2f);

    void SpawnDamageNumber(Vector3 world, string text, Color color, float size)
    {
        var d = damagePool.Count > 0 ? damagePool.Pop() : null;
        if (d == null)
        {
            d = new DamageNumber { text = NewText("Dmg", damageLayer, "", 30, Color.white) };
            d.text.fontStyle = FontStyles.Bold;
            Outline(d.text);
            var r = d.text.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.sizeDelta = new Vector2(120f, 50f);
        }
        d.text.gameObject.SetActive(true);
        d.text.text = text;
        d.text.color = color;
        d.text.fontSize = size;
        d.world = world;
        d.age = 0f;
        d.xDrift = Random.Range(-40f, 40f);
        damageNumbers.Add(d);
    }

    void UpdateDamageNumbers(float dt)
    {
        if (cam == null) return;
        Vector2 size = canvasRect.rect.size;
        for (int i = damageNumbers.Count - 1; i >= 0; i--)
        {
            var d = damageNumbers[i];
            d.age += dt;
            const float life = 0.85f;
            if (d.age >= life)
            {
                d.text.gameObject.SetActive(false);
                damageNumbers.RemoveAt(i);
                damagePool.Push(d);
                continue;
            }
            Vector3 vp = cam.WorldToViewportPoint(d.world);
            // The chase camera turns: a hit behind it would project mirrored onto the screen.
            d.text.enabled = vp.z > 0f;
            float rise = 40f * d.age + 30f * (1f - Mathf.Exp(-d.age * 10f));
            d.text.rectTransform.anchoredPosition = new Vector2(vp.x * size.x + d.xDrift * d.age, vp.y * size.y + rise);
            float pop = d.age < 0.1f ? Mathf.Lerp(0.4f, 1.4f, d.age / 0.1f) : Mathf.Lerp(1.4f, 1f, (d.age - 0.1f) * 6f);
            d.text.rectTransform.localScale = Vector3.one * pop;
            d.text.alpha = d.age < life * 0.6f ? 1f : 1f - (d.age - life * 0.6f) / (life * 0.4f);
        }
    }

    void ShowResults(int placement)
    {
        resultsPanel.SetActive(true);
        if (resultsButtons.childCount == 0 && Menus.Instance != null)
        {
            var buttonSize = new Vector2(250f, 72f);
            UIKit.PaintedButton(resultsButtons, "btn_yellow_big", "PLAY AGAIN", new Color(0.16f, 0.1f, 0.03f), new Vector2(0.5f, 0.5f), new Vector2(-135f, 0f),
                buttonSize, 28f, () => { if (MatchManager.Instance != null) MatchManager.Instance.PlayAgain(); });
            UIKit.PaintedButton(resultsButtons, "btn_teal_big", "MAIN MENU", new Color(0.04f, 0.17f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(135f, 0f),
                buttonSize, 28f, () => { if (MatchManager.Instance != null) MatchManager.Instance.BackToMenu(); });
        }
        bool trophy = placement == 1 && Menus.Instance != null;
        if (trophy && resultsTrophy == null)
        {
            resultsRays = NewImage("TrophyRays", resultsCard, UISprites.Rays, new Color(1f, 0.85f, 0.35f, 0.55f));
            Anchor(resultsRays.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 62f), new Vector2(320f, 320f));
            var glow = NewImage("TrophyGlow", resultsCard, UISprites.Glow, new Color(1f, 0.8f, 0.3f, 0.75f));
            Anchor(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 62f), new Vector2(220f, 220f));
            resultsTrophy = Menus.Instance.MakeTrophy(resultsCard, new Vector2(170f, 170f));
            Anchor(resultsTrophy.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 72f), new Vector2(170f, 170f));
        }
        // (The trophy sits above the card's top edge, clear of "YOU PLACED".)
        resultsAge = 0f;
        bool won = placement == 1;
        resultsLabel.text = placement > 0 ? "YOU PLACED" : "MATCH OVER";
        resultsTitle.text = placement > 0 ? MatchManager.Ordinal(placement) : "";
        resultsTitle.color = won ? GoldBar : placement == 2 ? new Color(0.85f, 0.88f, 0.92f) : placement == 3 ? new Color(0.9f, 0.6f, 0.35f) : Color.white;
        var mm = MatchManager.Instance;
        string winner = mm != null && mm.Winner != null && !mm.Winner.isPlayer ? $"<color=#{ColorUtility.ToHtmlStringRGB(mm.Winner.color)}>{mm.Winner.displayName}</color> won the match.\n" : "";
        resultsSub.text = (won ? "Last car standing - the luck is yours!\n" : winner) + $"Wins: {MatchManager.Wins}";
    }
}
