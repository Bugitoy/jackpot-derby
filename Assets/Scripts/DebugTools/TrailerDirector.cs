#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a scripted ~60 s trailer of a match, shot by shot: the player's car drives itself, chests,
/// pickups, cats and rivals are staged on cue, the storm is fast-forwarded, and painted text cards
/// explain each mechanic. Started (and recorded) by the editor menu Jackpot Derby/Record Trailer
/// (TrailerRecorder). Editor-only: never part of a game build.
/// </summary>
public class TrailerDirector : MonoBehaviour
{
    public static bool Finished { get; private set; }
    public static Action<float> StillRequested; // for dry runs: grab a frame at this trailer time

    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    CarIdentity player;
    CarMotor motor;
    ScriptedCarInput input;
    Odometer odo;
    Camera cam;
    ChaseCamera chase;

    Canvas canvas;
    Image fader;
    RectTransform cardRoot;
    float t0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Finished = false; StillRequested = null; }

    public float TrailerTime => Time.time - t0;

    void Start()
    {
        Finished = false;
        cam = Camera.main;
        chase = cam.GetComponent<ChaseCamera>();
        BuildOverlay();
        StartCoroutine(Run());
    }

    // =====================================================================================
    // The cut
    // =====================================================================================

    IEnumerator Run()
    {
        t0 = Time.time;
        Prepare();

        // 1. Logo over the city (the title orbit, menus hidden).
        SetFade(1f);
        var logo = Logo(new Vector2(0f, 90f), 1050f);
        var tag = Caption("Roll your lucky number.  Crack the chests.  Be the last car standing.", 34f, new Vector2(0f, -150f), false);
        yield return Fade(0f, 0.6f);
        yield return Wait(3.4f);
        yield return Fade(1f, 0.35f);
        Destroy(logo.gameObject); Destroy(tag.gameObject);

        // Into the match: skip the swoop, short countdown, start on the long north street.
        SetField(FindAnyObjectByType<Menus>(), "swoopTime", 0.02f);
        SetField(MatchManager.Instance, "countdownSeconds", 1.4f);
        FindAnyObjectByType<Menus>().GetType().GetMethod("Play", Any).Invoke(FindAnyObjectByType<Menus>(), null);
        while (MatchManager.Instance.WaitingForMenu) yield return null;
        yield return null;
        SetupCars();
        PlacePlayer(new Vector3(-84f, 0f, 46.8f), Vector3.right);
        yield return Fade(0f, 0.3f);
        while (MatchManager.Instance.State != MatchState.Playing) yield return null;
        SetupCars();   // again: the match start hands cars back to their drivers

        // 2. Drive: the odometer rolls.
        StartCoroutine(Card("YOUR NUMBER IS YOUR ODOMETER", 0.6f, 2.8f));
        Chest chest = null;
        float lane = 46.8f;
        while (player.transform.position.x < -44f) { Steer(new Vector3(player.transform.position.x + 14f, 0f, lane), 1f); yield return null; }

        // 3. A chest drops in at the crossroads ahead.
        chest = SpawnChest(new Vector3(0f, 0f, 48f), false);
        StartCoroutine(Card("CHESTS HIDE A LUCKY NUMBER", 0.3f, 2.6f));
        while (player.transform.position.x < -24f) { Steer(new Vector3(player.transform.position.x + 14f, 0f, lane), 1f); yield return null; }
        // Brake to a stop; the number locks and gets checked against the chest.
        odo.HoldHints = true;
        while (motor.ForwardSpeed > 0.3f) { Steer(new Vector3(player.transform.position.x + 14f, 0f, lane), -1f); yield return null; }
        input.throttle = 0f;
        yield return Wait(0.5f);   // stopped: the number locks (hint held back)
        SetChestNumber(chest, Odometer.Wrap(odo.Number - 16));
        odo.Lock(); odo.RevealHintAgainstNearestChest();
        odo.HoldHints = false;
        yield return Wait(0.3f);
        StartCoroutine(Card("STOP TO LOCK IT", 0f, 2.2f));
        yield return Wait(2.0f);
        // Back up a little: the number rolls down. Stop again: CLOSE.
        odo.Unlock();
        odo.HoldHints = true;
        float until = Time.time + 1.2f;
        while (Time.time < until) { input.throttle = -1f; input.steer = 0f; yield return null; }
        while (Mathf.Abs(motor.ForwardSpeed) > 0.3f) { input.throttle = 0.6f; yield return null; }
        input.throttle = 0f;
        yield return Wait(0.5f);
        SetChestNumber(chest, Odometer.Wrap(odo.Number - 3));
        odo.Lock(); odo.RevealHintAgainstNearestChest();
        odo.HoldHints = false;
        yield return Wait(0.4f);
        StartCoroutine(Card("CLOSE ENOUGH?  CRACK IT!", 0f, 2.0f));
        yield return Wait(1.3f);

        // 4. Ram the chest: a blaster.
        Vector3 chestPos = chest.transform.position;
        until = Time.time + 3.5f;
        while (chest != null && Chest.Active.Contains(chest) && Time.time < until) { Steer(chestPos, 1f); yield return null; }
        StartCoroutine(Card("WIN A BLASTER", 0.4f, 2.0f));
        until = Time.time + 1.4f;
        while (Time.time < until) { Steer(new Vector3(player.transform.position.x + 14f, 0f, lane), 0.55f); yield return null; }

        // 5. A rival comes the other way: auto-fire.
        var lucky8 = Enemy("Lucky8");
        Place(lucky8, new Vector3(player.transform.position.x + 30f, 0f, 49.6f), Vector3.left);
        lucky8.GetComponent<CarLoadout>().SetTier(BlasterTier.Small);
        var l8in = Scripted(lucky8);
        StartCoroutine(Card("BLASTERS FIRE ON THEIR OWN", 0.6f, 2.8f));
        until = Time.time + 4.2f;
        while (Time.time < until)
        {
            Steer(new Vector3(player.transform.position.x + 14f, 0f, lane), 0.35f);
            DriveScripted(lucky8, l8in, new Vector3(lucky8.transform.position.x - 14f, 0f, 49.6f), 0.3f);
            yield return null;
        }
        l8in.throttle = 0f;

        // Turbo, then T-bone a car parked across the road.
        var cat13 = Enemy("BlackCat13");
        Place(cat13, new Vector3(player.transform.position.x + 34f, 0f, 47.2f), Vector3.forward);
        Scripted(cat13);
        player.GetComponent<TurboAbility>().TryActivate();
        StartCoroutine(Card("TURBO!", 0f, 1.2f));
        until = Time.time + 3.2f;
        bool rammed = false;
        float hpBefore = cat13.GetComponent<CarHealth>().Health;
        while (Time.time < until)
        {
            Steer(new Vector3(cat13.transform.position.x, 0f, lane), 1f);
            if (!rammed && cat13.GetComponent<CarHealth>().Health < hpBefore) { rammed = true; StartCoroutine(Card("OR JUST RAM THEM", 0f, 2.0f)); }
            yield return null;
        }
        input.throttle = 0f;
        yield return Wait(0.5f);

        // 6. Dice and a black cat on the south street.
        yield return Fade(1f, 0.3f);
        ClearPickups();
        float south = -49.2f;
        PlacePlayer(new Vector3(-84f, 0f, south), Vector3.right);
        SpawnPickup("dicePrefab", new Vector3(-58f, 0f, south));
        yield return Fade(0f, 0.3f);
        bool diceCard = false, catCard = false;
        Vector3 catSpot = new Vector3(-22f, 0f, south);
        var cat = SpawnCat(catSpot);
        Action<GameObject, int> onScramble = (car, n) => { if (car == player.gameObject && !catCard) { catCard = true; StartCoroutine(Card("BLACK CATS DON'T", 0.1f, 2.0f)); } };
        BlackCat.AnyScrambled += onScramble;
        until = Time.time + 7.0f;
        while (Time.time < until)
        {
            Steer(new Vector3(player.transform.position.x + 14f, 0f, south), 0.8f);
            if (!diceCard && player.transform.position.x > -60f) { diceCard = true; StartCoroutine(Card("DICE BRING LUCK", 0.2f, 2.0f)); }
            if (cat != null && !catCard) HoldCat(cat, catSpot);
            yield return null;
        }
        BlackCat.AnyScrambled -= onScramble;
        input.throttle = 0f;

        // 7. The storm: caught outside the safe zone, race back in.
        yield return Fade(1f, 0.3f);
        var storm = StormController.Instance;
        SetMatchTime(60f + 30f * 2f + 8f);
        yield return null;
        Rect zone = storm.Zone;
        Vector3 sc = storm.SafeCentre;
        int park = 0;
        foreach (var ci in CarIdentity.All)
        {
            if (ci == player) continue;
            var h = ci.GetComponent<CarHealth>();
            if (h != null && h.IsAlive) h.RepairFull();
            Place(ci, sc + new Vector3(-20f, 0f, -12f + 12f * park++), Vector3.right);
        }
        PlacePlayer(new Vector3(zone.xMax + 7f, 0f, sc.z + 1.5f), Vector3.left);
        yield return Fade(0f, 0.3f);
        StartCoroutine(Card("THE BAD LUCK STORM CLOSES IN", 0.5f, 3.0f));
        until = Time.time + 5.0f;
        while (Time.time < until)
        {
            SetMatchTime(MatchManager.Instance.MatchTime + Time.deltaTime * 0.6f);
            Steer(new Vector3(player.transform.position.x - 14f, 0f, sc.z + 1.5f), Time.time > until - 4.0f ? 1f : 0f);
            yield return null;
        }
        input.throttle = 0f;

        // 8. Finale: Jackpot cannon takedown, last car standing.
        yield return Fade(1f, 0.3f);
        Vector3 arena = storm.SafeCentre;
        Vector3 dir = Mathf.Abs(arena.x) > 1f ? new Vector3(-Mathf.Sign(arena.x), 0f, 0f) : Vector3.right;
        PlacePlayer(arena - dir * 9f, dir);
        player.GetComponent<CarLoadout>().SetTier(BlasterTier.Jackpot);
        var dice11 = Enemy("DoubleDice11");
        Place(dice11, arena + dir * 4f, Vector3.Cross(Vector3.up, dir));
        Scripted(dice11);
        var h11 = dice11.GetComponent<CarHealth>();
        if (h11.Health > 48f) h11.TakeDamage(h11.Health - 48f, null, DamageKind.Other, dice11.transform.position);
        foreach (var e in new[] { "Lucky8", "BlackCat13" })
        {
            var en = Enemy(e);
            if (en != null) Place(en, arena - dir * 22f + Vector3.Cross(Vector3.up, dir) * (e == "Lucky8" ? 6f : -6f), dir);
        }
        yield return Fade(0f, 0.3f);
        StartCoroutine(Card("JACKPOT CANNON", 0.2f, 1.6f));
        until = Time.time + 4.0f;
        while (Time.time < until && h11.IsAlive) { input.throttle = 0f; yield return null; }
        yield return Wait(0.6f);
        StartCoroutine(Card("LAST CAR STANDING WINS", 0f, 2.2f));
        foreach (var e in new[] { "Lucky8", "BlackCat13", "DoubleDice11" })
        {
            var en = Enemy(e);
            if (en != null && en.GetComponent<CarHealth>().IsAlive)
            {
                en.GetComponent<CarHealth>().TakeDamage(999f, player, DamageKind.Other, en.transform.position + Vector3.up * 2f);
                yield return Wait(0.45f);
            }
        }
        while (!MatchManager.Instance.ResultsVisible) yield return null;
        StopAllCardsShowing();
        yield return Wait(2.8f);

        // 9. End card.
        var dim = Panel(new Color(0.03f, 0.04f, 0.06f, 0f));
        float k = 0f;
        while (k < 1f) { k += Time.deltaTime / 0.5f; dim.color = new Color(0.03f, 0.04f, 0.06f, 0.97f * k); yield return null; }
        var hudCanvas = GameObject.Find("HUD Canvas");
        if (hudCanvas != null) hudCanvas.GetComponent<Canvas>().enabled = false;
        Logo(new Vector2(0f, 130f), 900f);
        var play = Caption("PLAY FREE ON ITCH.IO", 66f, new Vector2(0f, -150f), true);
        UIKit.GoldGradient(play);
        Caption("Unity Game Jam 2026  •  Theme: Lucky Number", 28f, new Vector2(0f, -235f), false);
        yield return Wait(4.2f);
        yield return Fade(1f, 0.8f);
        yield return Wait(0.3f);
        Finished = true;
    }

    // =====================================================================================
    // Staging
    // =====================================================================================

    void Prepare()
    {
        player = CarIdentity.Player;
        motor = player.GetComponent<CarMotor>();
        odo = player.GetComponent<Odometer>();
        foreach (var f in FindObjectsByType<FpsCounter>()) SetField(f, "visible", false);
        foreach (var d in FindObjectsByType<DebugCheats>()) d.enabled = false;
        var menuCanvas = GameObject.Find("Menus Canvas");
        if (menuCanvas != null) menuCanvas.GetComponent<Canvas>().enabled = false;
        var ps = FindAnyObjectByType<PickupSpawner>();
        if (ps != null) ps.enabled = false;
        StartCoroutine(KeepPlayerHealthy());
    }

    /// <summary>The rivals only do what the director tells them; the chest and pickup timers are off.</summary>
    void SetupCars()
    {
        input = player.GetComponent<ScriptedCarInput>();
        if (input == null) input = player.gameObject.AddComponent<ScriptedCarInput>();
        motor.SetInput(input);
        foreach (var ci in CarIdentity.All)
        {
            if (ci == player) continue;
            var brain = ci.GetComponent<AIBrain>();
            if (brain != null) brain.enabled = false;
            Scripted(ci);
        }
        var spawner = FindAnyObjectByType<ChestSpawner>();
        if (spawner != null) spawner.Running = false;
        foreach (var ch in Chest.Active.ToArray()) Destroy(ch.gameObject);
        ClearPickups();
    }

    IEnumerator KeepPlayerHealthy()
    {
        while (true)
        {
            var h = player != null ? player.GetComponent<CarHealth>() : null;
            if (h != null && h.IsAlive && h.Health < 70f) h.Heal(30f);
            yield return new WaitForSeconds(0.5f);
        }
    }

    void PlacePlayer(Vector3 pos, Vector3 forward)
    {
        motor.Teleport(new Vector3(pos.x, 0.02f, pos.z), Quaternion.LookRotation(forward));
        input.throttle = 0f; input.steer = 0f;
        if (chase != null)
        {
            // Snap the chase camera behind the car instead of swinging across the map.
            chase.enabled = false; chase.enabled = true;
            var snap = chase.GetType().GetMethod("Snap", Any);
            if (snap != null) snap.Invoke(chase, null);
        }
    }

    static void Place(CarIdentity car, Vector3 pos, Vector3 forward)
    {
        if (car == null) return;
        car.GetComponent<CarMotor>().Teleport(new Vector3(pos.x, 0.02f, pos.z), Quaternion.LookRotation(forward));
    }

    static CarIdentity Enemy(string key)
    {
        foreach (var ci in CarIdentity.All) if (!ci.isPlayer && ci.name.Contains(key)) return ci;
        return null;
    }

    static ScriptedCarInput Scripted(CarIdentity car)
    {
        var s = car.GetComponent<ScriptedCarInput>();
        if (s == null) s = car.gameObject.AddComponent<ScriptedCarInput>();
        s.throttle = 0f; s.steer = 0f;
        car.GetComponent<CarMotor>().SetInput(s);
        var driver = car.GetComponent<AICarDriver>();
        if (driver != null) driver.StopCar();
        return s;
    }

    /// <summary>Pure-pursuit steering toward a point, with the given throttle.</summary>
    void Steer(Vector3 target, float throttle)
    {
        input.throttle = throttle;
        input.steer = SteerToward(player.transform, target, motor.ForwardSpeed);
    }

    static void DriveScripted(CarIdentity car, ScriptedCarInput s, Vector3 target, float throttle)
    {
        s.throttle = throttle;
        s.steer = SteerToward(car.transform, target, car.GetComponent<CarMotor>().ForwardSpeed);
    }

    static float SteerToward(Transform t, Vector3 target, float speed)
    {
        Vector3 to = target - t.position; to.y = 0f;
        Vector3 f = t.forward; f.y = 0f;
        float angle = Vector3.SignedAngle(f, to, Vector3.up);
        if (speed < -0.5f) angle = -angle;
        return Mathf.Clamp(angle / 22f, -1f, 1f);
    }

    Chest SpawnChest(Vector3 pos, bool jackpot)
    {
        var spawner = FindAnyObjectByType<ChestSpawner>();
        var prefab = (Chest)GetField(spawner, jackpot ? "jackpotChestPrefab" : "chestPrefab");
        return Instantiate(prefab, pos, Quaternion.Euler(0f, -90f, 0f));
    }

    static void SetChestNumber(Chest c, int n)
    {
        var f = typeof(Chest).GetField("<Number>k__BackingField", Any);
        if (f != null && c != null) f.SetValue(c, n);
    }

    void SpawnPickup(string field, Vector3 pos)
    {
        var ps = FindAnyObjectByType<PickupSpawner>(FindObjectsInactive.Include);
        var prefab = (Pickup)GetField(ps, field);
        Instantiate(prefab, pos, Quaternion.identity);
    }

    BlackCat SpawnCat(Vector3 pos)
    {
        var ps = FindAnyObjectByType<PickupSpawner>(FindObjectsInactive.Include);
        var prefab = (BlackCat)GetField(ps, "catPrefab");
        var cat = Instantiate(prefab, pos, Quaternion.LookRotation(Vector3.left));
        var agent = cat.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = true;   // sits in the road, waiting to be hit
        return cat;
    }

    /// <summary>Keep the cat sitting in the road until a car hits it.</summary>
    static void HoldCat(BlackCat cat, Vector3 spot)
    {
        var agent = cat.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) { agent.Warp(spot); agent.isStopped = true; agent.velocity = Vector3.zero; }
        else cat.transform.position = spot;
        cat.transform.rotation = Quaternion.LookRotation(Vector3.left);
    }

    static void ClearPickups()
    {
        foreach (var p in FindObjectsByType<Pickup>()) Destroy(p.gameObject);
        foreach (var c in FindObjectsByType<BlackCat>()) Destroy(c.gameObject);
    }

    static void SetMatchTime(float t)
    {
        var f = typeof(MatchManager).GetField("<MatchTime>k__BackingField", Any);
        if (f != null) f.SetValue(MatchManager.Instance, t);
    }

    static object GetField(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any);
            if (f != null) return f.GetValue(o);
        }
        return null;
    }

    static void SetField(object o, string name, object value)
    {
        if (o == null) return;
        for (var t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any);
            if (f != null) { f.SetValue(o, value); return; }
        }
    }

    IEnumerator Wait(float s) { float end = Time.time + s; while (Time.time < end) yield return null; }

    // =====================================================================================
    // Overlay: fades, text cards, logo
    // =====================================================================================

    void BuildOverlay()
    {
        var go = new GameObject("TrailerOverlay");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cardRoot = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
        cardRoot.SetParent(go.transform, false);
        Stretch(cardRoot);
        fader = Panel(Color.black);
        fader.transform.SetAsLastSibling();
    }

    Image Panel(Color c)
    {
        var img = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(canvas.transform, false);
        Stretch(img.rectTransform);
        img.color = c;
        img.raycastTarget = false;
        if (fader != null) fader.transform.SetAsLastSibling();
        return img;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void SetFade(float a) => fader.color = new Color(0f, 0f, 0f, a);

    IEnumerator Fade(float to, float seconds)
    {
        float from = fader.color.a, k = 0f;
        while (k < 1f) { k += Time.deltaTime / Mathf.Max(0.01f, seconds); SetFade(Mathf.Lerp(from, to, k)); yield return null; }
        SetFade(to);
    }

    RectTransform Logo(Vector2 pos, float width)
    {
        var img = new GameObject("Logo", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(canvas.transform, false);
        img.sprite = UIKit.Art("logo");
        img.preserveAspect = true;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(width, width * 0.5f);
        r.anchoredPosition = pos;
        fader.transform.SetAsLastSibling();
        StartCoroutine(Pop(r, 0.45f));
        return r;
    }

    TextMeshProUGUI Caption(string text, float size, Vector2 pos, bool bold)
    {
        var t = UIKit.Label(canvas.transform, text, size, Color.white, bold ? UIWeight.ExtraBold : UIWeight.Bold, true);
        t.alignment = TextAlignmentOptions.Center;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(1800f, size * 1.6f);
        r.anchoredPosition = pos;
        fader.transform.SetAsLastSibling();
        return t;
    }

    /// <summary>A painted-style caption that pops in low on screen, holds, and fades.</summary>
    IEnumerator Card(string text, float delay, float hold)
    {
        if (delay > 0f) yield return Wait(delay);
        foreach (Transform old in cardRoot) Destroy(old.gameObject);
        var t = UIKit.Label(cardRoot, text, 78f, Color.white, UIWeight.ExtraBold, true);
        UIKit.GoldGradient(t);
        t.alignment = TextAlignmentOptions.Center;
        t.characterSpacing = 2f;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(1800f, 120f);
        r.anchoredPosition = new Vector2(0f, 400f);
        StartCoroutine(Pop(r, 0.32f));
        yield return Wait(hold);
        float k = 0f;
        while (k < 1f && t != null) { k += Time.deltaTime / 0.25f; t.alpha = 1f - k; yield return null; }
        if (t != null) Destroy(t.gameObject);
    }

    void StopAllCardsShowing()
    {
        foreach (Transform old in cardRoot) Destroy(old.gameObject);
    }

    IEnumerator Pop(RectTransform r, float seconds)
    {
        float k = 0f;
        while (k < 1f && r != null)
        {
            k += Time.deltaTime / seconds;
            float s = k < 1f ? Mathf.LerpUnclamped(0.6f, 1f, 1f - Mathf.Pow(1f - k, 3f)) + 0.12f * Mathf.Sin(k * Mathf.PI) : 1f;
            r.localScale = Vector3.one * s;
            yield return null;
        }
        if (r != null) r.localScale = Vector3.one;
    }

    void LateUpdate()
    {
        StillRequested?.Invoke(TrailerTime);
    }
}
#endif
