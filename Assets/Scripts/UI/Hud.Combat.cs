using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Combat half of the HUD: player health + blaster bars, chunky bars floating over every car,
/// pop-up damage numbers, Turbo / Jackpot Blast cooldown icons, countdown, match timer,
/// kill feed, hit vignette, 777 flash, BUSTED popup and the results screen.
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
    JackpotBlast pBlast;

    // Abilities
    Image turboFill, blastFill, turboBack, blastBack;
    TextMeshProUGUI turboKey, blastKey, blastIcon;

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
        public CarHealth health;
        public CarLoadout loadout;
        public float sinceHit = 99f;
        public float pop;
        public BlasterTier shownLayer;
    }
    readonly Dictionary<CarHealth, WorldBar> worldBars = new Dictionary<CarHealth, WorldBar>();
    RectTransform worldBarLayer, damageLayer;

    // Damage numbers
    class DamageNumber { public TextMeshProUGUI text; public Vector3 world; public float age; public float xDrift; }
    readonly List<DamageNumber> damageNumbers = new List<DamageNumber>();
    readonly Stack<DamageNumber> damagePool = new Stack<DamageNumber>();

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

        // Player bars, bottom-left.
        var panel = NewImage("PlayerBars", canvasRect, UISprites.RoundedRect, panelColor);
        Anchor(panel.rectTransform, new Vector2(0f, 0f), new Vector2(180f, 64f), new Vector2(330f, 100f));
        nameText = NewText("Name", panel.rectTransform, "LUCKY 7", 16, new Color(0.75f, 0.95f, 0.92f));
        nameText.alignment = TextAlignmentOptions.Left;
        Anchor(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -14f), new Vector2(200f, 22f));

        blasterRow = NewRect("BlasterRow", panel.rectTransform, Vector2.zero, Vector2.zero).gameObject;
        var br = (RectTransform)blasterRow.transform;
        Anchor(br, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(310f, 26f));
        blasterText = NewText("BlasterLabel", br, "SMALL", 14, Color.white);
        blasterText.alignment = TextAlignmentOptions.Left;
        Anchor(blasterText.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(100f, 22f));
        Outline(blasterText);
        MakeBar(br, new Vector2(54f, 0f), new Vector2(196f, 16f), GoldBar, out blasterFill, out blasterFillImg);

        var hpRow = NewRect("HpRow", panel.rectTransform, Vector2.zero, Vector2.zero);
        Anchor(hpRow, new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(310f, 30f));
        var hpLabel = NewText("HpLabel", hpRow, "HP", 18, Color.white);
        hpLabel.fontStyle = FontStyles.Bold;
        Anchor(hpLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(40f, 24f));
        Outline(hpLabel);
        MakeBar(hpRow, new Vector2(30f, 0f), new Vector2(200f, 22f), HpGreen, out hpFill, out hpFillImg);
        hpText = NewText("HpValue", hpRow, "100", 18, Color.white);
        hpText.fontStyle = FontStyles.Bold;
        Anchor(hpText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(50f, 24f));
        Outline(hpText);

        // Ability icons, bottom-right.
        MakeAbility(new Vector2(-170f, 72f), "TURBO", "SHIFT", TealBar, out turboBack, out turboFill, out turboKey, out _);
        MakeAbility(new Vector2(-80f, 72f), "777", "SPACE", GoldBar, out blastBack, out blastFill, out blastKey, out blastIcon);

        // Top: timer + cars left.
        timerText = NewText("MatchTimer", canvasRect, "0:00", 34, Color.white);
        timerText.fontStyle = FontStyles.Bold;
        Anchor(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(200f, 40f));
        Outline(timerText);
        carsLeftText = NewText("CarsLeft", canvasRect, "", 16, new Color(1f, 1f, 1f, 0.85f));
        Anchor(carsLeftText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(240f, 22f));
        Outline(carsLeftText);

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

        var card = NewImage("Card", dim.rectTransform, UISprites.RoundedRect, new Color(0.08f, 0.16f, 0.18f, 0.97f));
        resultsCard = card.rectTransform;
        Anchor(resultsCard, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(560f, 330f));
        resultsTitle = NewText("Title", resultsCard, "1ST PLACE!", 64, GoldBar);
        resultsTitle.fontStyle = FontStyles.Bold;
        Anchor(resultsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(540f, 80f));
        Outline(resultsTitle);
        resultsSub = NewText("Sub", resultsCard, "", 24, Color.white);
        Anchor(resultsSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(520f, 70f));
        resultsSub.textWrappingMode = TextWrappingModes.Normal;

        var buttonImg = NewImage("PlayAgain", resultsCard, UISprites.RoundedRect, GoldBar);
        buttonImg.raycastTarget = true;
        Anchor(buttonImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(300f, 70f));
        var button = buttonImg.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.75f);
        colors.pressedColor = new Color(0.85f, 0.7f, 0.3f);
        button.colors = colors;
        button.onClick.AddListener(() => { if (MatchManager.Instance != null) MatchManager.Instance.PlayAgain(); });
        var label = NewText("Label", buttonImg.rectTransform, "PLAY AGAIN", 32, new Color(0.15f, 0.1f, 0.05f));
        label.fontStyle = FontStyles.Bold;
        Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(300f, 50f));
        var hint = NewText("EnterHint", buttonImg.rectTransform, "press ENTER", 13, new Color(0.15f, 0.1f, 0.05f, 0.8f));
        Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(200f, 16f));

        resultsPanel.SetActive(false);

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---------------- binding ----------------

    void OnEnable()
    {
        CarHealth.AnyDamaged += OnAnyDamaged;
    }

    void OnDisable()
    {
        CarHealth.AnyDamaged -= OnAnyDamaged;
        if (Instance == this) Instance = null;
    }

    void BindPlayerCombat()
    {
        pHealth = player.GetComponent<CarHealth>();
        pTurbo = player.GetComponent<TurboAbility>();
        pBlast = player.GetComponent<JackpotBlast>();
        nameText.text = player.displayName.ToUpper();
        if (pHealth != null) pHealth.Damaged += _ => vignette = 0.55f;
        if (pBlast != null) pBlast.Activated += _ => On777();
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

    void OnPlayerBusted(BlasterTier newTier)
    {
        ShowPopup("BUSTED!", HpRed, newTier == BlasterTier.Small ? "demoted to the small blaster" : "blaster lost - find a chest!");
        CameraShake.Add(0.35f);
    }

    void On777()
    {
        ShowPopup("7  7  7", GoldBar, "JACKPOT BLAST!");
        flashAlpha = 0.45f;
        CameraShake.Add(0.55f);
    }

    void OnAnyDamaged(DamageInfo d)
    {
        if (d.victim != null && worldBars.TryGetValue(d.victim, out var bar)) { bar.sinceHit = 0f; bar.pop = 1f; }
        if (d.amount < 0.5f) return;
        bool victimIsPlayer = d.victim != null && d.victim.Identity != null && d.victim.Identity.isPlayer;
        bool attackerIsPlayer = d.attacker != null && d.attacker.isPlayer;
        Color c = victimIsPlayer ? HpRed : d.toBlaster > 0f && d.toHealth <= 0f ? TealBar : Color.white;
        if (d.kind == DamageKind.Ram || d.kind == DamageKind.Blast) c = victimIsPlayer ? HpRed : GoldBar;
        float size = d.kind == DamageKind.Ram || d.kind == DamageKind.Blast ? 40f : 30f;
        SpawnDamageNumber(d.point + Vector3.up * 1.5f, Mathf.CeilToInt(d.amount).ToString(), c, size);
        if (victimIsPlayer) CameraShake.Add(Mathf.Clamp(0.18f + d.amount / 50f, 0f, 0.6f));
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
            carsLeftText.text = mm.State == MatchState.Countdown ? "GET READY" : $"CARS LEFT  {mm.CarsLeft}";
            if (mm.ResultsVisible)
            {
                resultsAge += Time.unscaledDeltaTime;
                float s = resultsAge < 0.2f ? Mathf.Lerp(0.6f, 1.08f, resultsAge / 0.2f) : 1f + 0.08f * Mathf.Exp(-(resultsAge - 0.2f) * 8f) * Mathf.Cos((resultsAge - 0.2f) * 22f);
                resultsCard.localScale = Vector3.one * s;
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

        vignette = Mathf.Max(0f, vignette - dt * 1.6f);
        vignetteImg.color = new Color(1f, 0.1f, 0.1f, vignette);
        flashAlpha = Mathf.Max(0f, flashAlpha - dt * 1.8f);
        flashImg.color = new Color(1f, 0.9f, 0.5f, flashAlpha);
    }

    void UpdatePlayerPanel()
    {
        if (pHealth == null) return;
        float hp = pHealth.Health / pHealth.MaxHealth;
        SetFill(hpFill, hp);
        hpFillImg.color = HpColor(hp);
        hpText.text = Mathf.CeilToInt(pHealth.Health).ToString();

        bool armed = loadout != null && loadout.IsArmed;
        blasterRow.SetActive(armed);
        if (armed)
        {
            bool jackpot = loadout.Tier == BlasterTier.Jackpot;
            blasterText.text = jackpot ? "JACKPOT" : "SMALL";
            blasterText.color = jackpot ? GoldBar : TealBar;
            blasterFillImg.color = jackpot ? GoldBar : TealBar;
            SetFill(blasterFill, loadout.BlasterBar / Mathf.Max(1f, loadout.BlasterBarMax));
        }
    }

    void UpdateAbilities()
    {
        if (pTurbo != null)
        {
            turboFill.fillAmount = pTurbo.IsActive ? pTurbo.ActiveLeft / 2f : pTurbo.Readiness;
            float pulse = pTurbo.IsReady ? 1f + 0.05f * Mathf.Sin(Time.time * 5f) : 1f;
            turboBack.rectTransform.localScale = Vector3.one * (pTurbo.IsActive ? 1.12f : pulse);
            turboFill.color = pTurbo.IsReady || pTurbo.IsActive ? TealBar : new Color(0.3f, 0.5f, 0.5f);
        }
        if (pBlast != null)
        {
            bool available = pBlast.IsAvailable;
            blastBack.gameObject.SetActive(available);
            if (available)
            {
                blastFill.fillAmount = pBlast.Readiness;
                blastFill.color = pBlast.IsReady ? GoldBar : new Color(0.55f, 0.45f, 0.2f);
                float pulse = pBlast.IsReady ? 1f + 0.06f * Mathf.Sin(Time.time * 6f) : 1f;
                blastBack.rectTransform.localScale = Vector3.one * pulse;
            }
        }
    }

    void UpdateWorldBars()
    {
        if (cam == null) return;
        foreach (var id in CarIdentity.All)
        {
            var h = id.GetComponent<CarHealth>();
            if (h != null && !worldBars.ContainsKey(h)) worldBars[h] = MakeWorldBar(h, id);
        }
        Vector2 size = canvasRect.rect.size;
        float dt = Time.deltaTime;
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
            b.root.anchoredPosition = new Vector2(vp.x * size.x, vp.y * size.y);
            b.group.alpha = alpha;

            // Only the outermost layer the car still has.
            BlasterTier layer = b.loadout != null ? b.loadout.Tier : BlasterTier.None;
            if (layer != b.shownLayer) { b.shownLayer = layer; b.pop = 1.6f; }   // a layer just got shot away
            if (layer == BlasterTier.None)
            {
                float hp = b.health.Health / b.health.MaxHealth;
                SetFill(b.fill, hp);
                b.fillImg.color = HpColor(hp);
            }
            else
            {
                SetFill(b.fill, b.loadout.BlasterBar / Mathf.Max(1f, b.loadout.BlasterBarMax));
                b.fillImg.color = layer == BlasterTier.Jackpot ? GoldBar : TealBar;
            }

            b.pop = Mathf.Max(0f, b.pop - dt * 6f);
            b.root.localScale = Vector3.one * (1f + 0.12f * b.pop);
        }
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
        b.label = NewText("Name", b.root, id.isPlayer ? "YOU" : id.displayName.ToUpper(), 13, id.color);
        b.label.fontStyle = FontStyles.Bold;
        Anchor(b.label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(160f, 18f));
        Outline(b.label);
        MakeBar(b.root, Vector2.zero, new Vector2(88f, 16f), HpGreen, out b.fill, out b.fillImg);
        b.root.gameObject.SetActive(false);
        return b;
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
        resultsAge = 0f;
        bool won = placement == 1;
        resultsTitle.text = won ? "1ST PLACE!" : placement > 0 ? "YOU PLACED " + MatchManager.Ordinal(placement) : "MATCH OVER";
        resultsTitle.color = won ? GoldBar : placement == 2 ? new Color(0.85f, 0.88f, 0.92f) : placement == 3 ? new Color(0.9f, 0.6f, 0.35f) : Color.white;
        var mm = MatchManager.Instance;
        string winner = mm != null && mm.Winner != null && !mm.Winner.isPlayer ? $"\n<color=#{ColorUtility.ToHtmlStringRGB(mm.Winner.color)}>{mm.Winner.displayName}</color> won the match." : "";
        resultsSub.text = (won ? "LAST CAR STANDING - the luck is yours!" : "Better luck next time.") + winner + $"\nWins: {MatchManager.Wins}";
    }
}
