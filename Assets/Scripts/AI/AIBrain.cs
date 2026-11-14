using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI decision making: a small state machine (Wander, SeekChest, Hunt, Flee, Ram) on top of AICarDriver.
/// The AI plays the number game like a player: drive to change the number, stop to lock it and read the
/// hint, then adjust (a guessing search), with personality-driven mistakes and pauses.
/// Blasters fire on their own (AutoBlaster), so the brain only decides where to drive.
/// </summary>
[RequireComponent(typeof(AICarDriver))]
public class AIBrain : MonoBehaviour
{
    public enum State { Wander, SeekChest, Hunt, Flee, Ram }
    enum ChestPhase { Approach, Stopping, Thinking, Adjusting, Reversing, Ramming }

    [SerializeField] AIPersonality personality;
    [SerializeField] float thinkInterval = 0.25f;
    [SerializeField] float minStateTime = 0.8f;
    [Tooltip("Start the number game when this close to the chest.")]
    [SerializeField] float checkDistance = 30f;
    [Tooltip("Circle the chest at this radius while adjusting the number.")]
    [SerializeField] float chestOrbitRadius = 14f;

    public State Current { get; private set; } = State.Wander;
    public AIPersonality Personality => personality;

    AICarDriver driver;
    Odometer odo;
    CarLoadout loadout;
    CarHealth health;
    AutoBlaster blaster;
    TurboAbility turbo;
    CarMotor motor;

    float thinkTimer, stateTime, ramTimer, ramCooldown, fleeRepick, roamTimer, waitTimer, braveTimer;
    bool brave;
    CarHealth target, threat;
    Vector3 roamPoint, fleePoint;
    CarHealth huntingTarget, givenUpOn;
    float huntTime, givenUpUntil;
    bool stormEscape;
    Pickup pickupGoal;

    // Number game for the current chest.
    Chest chest, searchChest;
    ChestPhase phase;
    int lo = 1, hi = 99;
    float phaseTimer, adjustGoal, adjustStart, misjudge, orbitAngle, unlockTravelMark;
    readonly Dictionary<Chest, bool> wantsChest = new Dictionary<Chest, bool>();

    static readonly List<Vector3> roadPoints = new List<Vector3>();
    static readonly List<Vector3> openPoints = new List<Vector3>();
    static readonly List<Vector3> cornerPoints = new List<Vector3>();
    static ArenaGrid pointsGrid;
    static int losMask = -1;

    void Awake()
    {
        driver = GetComponent<AICarDriver>();
        odo = GetComponent<Odometer>();
        loadout = GetComponent<CarLoadout>();
        health = GetComponent<CarHealth>();
        blaster = GetComponent<AutoBlaster>();
        turbo = GetComponent<TurboAbility>();
        motor = GetComponent<CarMotor>();
        if (personality == null) personality = ScriptableObject.CreateInstance<AIPersonality>();
        if (losMask == -1)
        {
            int carLayer = LayerMask.NameToLayer("Car");
            losMask = Physics.DefaultRaycastLayers & ~(carLayer >= 0 ? 1 << carLayer : 0);
        }
        orbitAngle = Random.Range(0f, 360f);
    }

    void OnEnable()
    {
        if (odo != null) odo.HintRevealed += OnHint;
        var ram = GetComponent<RamDamage>();
        if (ram != null) RamDamage.AnyRam += OnRam;
    }

    void OnDisable()
    {
        if (odo != null) odo.HintRevealed -= OnHint;
        RamDamage.AnyRam -= OnRam;
    }

    /// <summary>Log AI decisions to the console (for tuning).</summary>
    public static bool LogDecisions;
    void Log(string msg) { if (LogDecisions) Debug.Log($"[AI {name}] {msg}"); }

    // After landing a ram, back off instead of shoving again and again.
    void OnRam(RamDamage rammer, RamDamage victim, float damage, bool side)
    {
        if (rammer.gameObject == gameObject && Current == State.Ram) { ramTimer = 0f; Log($"rammed {victim.name} for {damage:0}"); }
    }

    void Update()
    {
        if (health != null && !health.IsAlive) { driver.StopCar(); enabled = false; return; }
        var mm = MatchManager.Instance;
        if (mm != null && mm.State != MatchState.Playing) { driver.StopCar(); return; }

        float dt = Time.deltaTime;
        stateTime += dt;
        ramCooldown -= dt;
        thinkTimer -= dt;
        braveTimer -= dt;
        if (braveTimer <= 0f) { braveTimer = 3f; brave = Random.value < personality.riskTaking * 0.5f; }
        if (thinkTimer <= 0f) { thinkTimer = thinkInterval * Random.Range(0.8f, 1.2f); Think(); }
        Act(dt);
    }

    // ================= deciding =================

    void Think()
    {
        EnsurePoints();
        var p = personality;
        bool armed = loadout.IsArmed;
        float hp = health.Health / health.MaxHealth;
        threat = FindThreat();
        target = FindHuntTarget();
        chest = PickChest();

        // The storm beats everything: get inside before it closes in.
        var storm = StormController.Instance;
        stormEscape = storm != null && !storm.IsInsideTarget(transform.position, 4f)
                      && (!storm.IsInside(transform.position, 2f) || storm.IsShrinking || storm.TimeToNextShrink < 10f);
        pickupGoal = PickPickup(hp);

        State next;
        if (Current == State.Ram && ramTimer > 0f && target != null) next = State.Ram;
        else if (threat != null && (!armed || hp < p.fleeHealth) && !brave && !CommittedToChest()) next = State.Flee;
        else if (ramCooldown <= 0f && TryPickRamTarget(out var ramTarget)) { next = State.Ram; target = ramTarget; ramTimer = 2.5f; }
        else if (armed && target != null && (chest == null || !WantsChest(chest) || Dist(target.transform.position) < blaster.Range * 1.3f)) next = State.Hunt;
        else if (chest != null && WantsChest(chest)) next = State.SeekChest;
        else next = State.Wander;

        // Double Dice style chaos: the odd random change of plan.
        if (p.chaos > 0f && Random.value < p.chaos * 0.04f && next != State.Flee)
        {
            var options = new List<State> { State.Wander };
            if (target != null) options.Add(State.Hunt);
            if (chest != null) options.Add(State.SeekChest);
            next = options[Random.Range(0, options.Count)];
        }

        if (next != Current && (stateTime > minStateTime || next == State.Flee || next == State.Ram)) SetState(next);
    }

    void SetState(State s)
    {
        if (s != Current) Log($"{Current} -> {s}");
        Current = s;
        stateTime = 0f;
        roamTimer = 0f;
        fleeRepick = 0f;
        waitTimer = 0f;
    }

    bool CommittedToChest() => Current == State.SeekChest && phase == ChestPhase.Ramming && chest != null && Dist(chest.transform.position) < 15f;

    bool WantsChest(Chest c)
    {
        if (!loadout.IsArmed || loadout.Tier == BlasterTier.Small) return true;
        if (health.Health < health.MaxHealth * 0.6f) return true;   // on the cannon, a chest = full repair
        if (!wantsChest.TryGetValue(c, out bool wants))
        {
            wants = Random.value < personality.chestGreed;
            wantsChest[c] = wants;
        }
        return wants;
    }

    /// <summary>Clovers when hurt (everyone); dice for gamblers (Double Dice), or anyone near one while a chest is out.</summary>
    Pickup PickPickup(float hp)
    {
        Pickup best = null;
        float bestD = float.MaxValue;
        var storm = StormController.Instance;
        foreach (var pk in Pickup.Active)
        {
            if (pk == null) continue;
            float d = Dist(pk.transform.position);
            // Dice stack luck toward a chest: gamblers always want them, everyone grabs a close one while a chest is out.
            bool wants = pk.Kind == PickupKind.Clover ? hp < 0.7f : personality.chaos > 0.3f || (Chest.Active.Count > 0 && d < 22f);
            if (!wants) continue;
            if (d > 50f || (storm != null && !storm.IsInside(pk.transform.position, 2f))) continue;
            if (d < bestD) { bestD = d; best = pk; }
        }
        return best;
    }

    Chest PickChest()
    {
        Chest best = null;
        float bestD = float.MaxValue;
        var storm = StormController.Instance;
        foreach (var c in Chest.Active)
        {
            if (storm != null && !storm.IsInside(c.transform.position, 2f)) continue;
            float d = Dist(c.transform.position);
            if (c == chest) d *= 0.6f; // stick with the chest we're already working on
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    CarHealth FindThreat()
    {
        CarHealth best = null;
        float bestD = float.MaxValue;
        foreach (var other in CarHealth.Alive)
        {
            if (other == health) continue;
            var ol = other.GetComponent<CarLoadout>();
            var ob = other.GetComponent<AutoBlaster>();
            if (ol == null || !ol.IsArmed || ob == null) continue;
            float d = Dist(other.transform.position);
            if (d > ob.Range + 7f || d >= bestD || !CanSee(other)) continue;
            best = other;
            bestD = d;
        }
        return best;
    }

    CarHealth FindHuntTarget()
    {
        float radius = personality.huntRadius * (0.6f + 0.4f * personality.aggression);
        CarHealth best = null;
        float bestScore = float.MaxValue;
        foreach (var other in CarHealth.Alive)
        {
            if (other == health) continue;
            if (other == givenUpOn && Time.time < givenUpUntil) continue;   // lost interest in this one for now
            float d = Dist(other.transform.position);
            if (d > radius || !CanSee(other)) continue;
            float score = other.Health + d * 0.5f; // weakest first, nearer breaks ties
            if (score < bestScore) { bestScore = score; best = other; }
        }
        return best;
    }

    bool TryPickRamTarget(out CarHealth ramTarget)
    {
        ramTarget = null;
        var p = personality;
        // Rams are a big deal (they hurt us too): no ramming while working a chest, and only good targets.
        if (Current == State.SeekChest && phase != ChestPhase.Approach) return false;
        bool armed = loadout.IsArmed;
        foreach (var other in CarHealth.Alive)
        {
            if (other == health) continue;
            float d = Dist(other.transform.position);
            if (d > p.ramDistance || !CanSee(other)) continue;
            bool weak = other.Health / other.MaxHealth < p.ramWeakHealth;
            Vector3 toMe = transform.position - other.transform.position; toMe.y = 0f;
            float sideAngle = Vector3.Angle(Flat(other.transform.forward), toMe);
            bool exposed = sideAngle > 50f;                       // side or rear facing us
            bool sideLover = p.sideRamLove > 0.8f;
            // Armed cars let their blaster do the work; rams are for finishing weak cars, or Black Cat's side-rams.
            bool worthIt = weak || (exposed && sideLover);
            if (!worthIt) continue;
            float chance = (weak ? 0.25f : 0.08f + p.sideRamLove * 0.12f) * (0.3f + p.aggression * 0.7f) * (armed ? 0.6f : 1f);
            if (Random.value < chance) { ramTarget = other; return true; }
        }
        return false;
    }

    // ================= acting =================

    void Act(float dt)
    {
        var p = personality;
        // Swerve when someone armed is close, so their bolts can miss.
        driver.Weave = threat != null && Dist(threat.transform.position) < 20f && Current != State.Ram ? 0.55f : 0f;

        if (stormEscape && StormController.Instance != null)
        {
            driver.GoTo(StormController.Instance.ClosestSafePoint(transform.position, 10f), 1f);
            MaybeTurbo(1f);
            return;
        }
        if (pickupGoal != null && (Current == State.Wander || (Current == State.Flee && Dist(pickupGoal.transform.position) < 15f)))
        {
            driver.GoTo(pickupGoal.transform.position, 1f);
            return;
        }

        switch (Current)
        {
            case State.Wander:
                if (p.ambusher) Ambush(dt);
                else
                {
                    roamTimer -= dt;
                    if (roamTimer <= 0f || driver.DistanceTo(roamPoint) < 6f)
                    {
                        roamTimer = Random.Range(6f, 10f);
                        roamPoint = RandomPoint(Random.value < 0.5f ? openPoints : roadPoints);
                    }
                    driver.GoTo(roamPoint, p.cruiseSpeed);
                }
                MaybeTurbo(0.15f);
                break;

            case State.Flee:
                fleeRepick -= dt;
                if (fleeRepick <= 0f || driver.DistanceTo(fleePoint) < 6f) { fleeRepick = 1.5f; fleePoint = PickFleePoint(); }
                driver.GoTo(fleePoint, 1f);
                if (threat != null && Dist(threat.transform.position) < 12f) MaybeTurbo(1.5f);
                break;

            case State.Hunt:
                if (target == null) { SetState(State.Wander); break; }
                // Chase budget: after a while, give up on this target and go do something else.
                if (target != huntingTarget) { huntingTarget = target; huntTime = 0f; }
                huntTime += dt;
                if (huntTime > 4f + p.aggression * 4f)
                {
                    givenUpOn = target;
                    givenUpUntil = Time.time + 12f - p.aggression * 4f;
                    Log($"gives up chasing {target.name}");
                    target = null;
                    SetState(chest != null ? State.SeekChest : State.Wander);
                    break;
                }
                Hunt();
                break;

            case State.Ram:
                ramTimer -= dt;
                if (target == null || !target.IsAlive || ramTimer <= 0f) { ramCooldown = 6f; SetState(State.Wander); break; }
                var tb = target.GetComponent<Rigidbody>();
                Vector3 lead = tb != null ? tb.linearVelocity * 0.3f : Vector3.zero;
                driver.Chase(target.transform.position + lead, 1f);
                if (Dist(target.transform.position) < 20f) MaybeTurbo(2f);
                break;

            case State.SeekChest:
                ChestGame(dt);
                break;
        }
    }

    void Hunt()
    {
        float range = blaster != null && loadout.IsArmed ? blaster.Range : 12f;
        Vector3 tp = target.transform.position;
        float d = Dist(tp);
        if (d > range * 0.7f)
        {
            driver.GoTo(tp, 1f);
            if (d > range * 1.2f) MaybeTurbo(0.4f);
        }
        else
        {
            // Inside range: keep circling the target so we're not a sitting duck.
            Vector3 away = transform.position - tp; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away);
            driver.GoTo(tp + away * range * 0.55f + side * 7f, 0.75f);
        }
    }

    void Ambush(float dt)
    {
        // Black Cat: lurk at a street corner near the closest enemy; Think() springs the side-ram.
        if (waitTimer > 0f)
        {
            waitTimer -= dt;
            driver.StopCar();
            return;
        }
        roamTimer -= dt;
        if (roamTimer <= 0f)
        {
            roamTimer = Random.Range(8f, 12f);
            var enemy = NearestEnemy();
            roamPoint = enemy != null ? NearestTo(cornerPoints, enemy.transform.position, 1) : RandomPoint(cornerPoints);
        }
        if (driver.DistanceTo(roamPoint) < 5f) { waitTimer = Random.Range(3f, 5f); roamTimer = waitTimer + 0.1f; }
        else driver.GoTo(roamPoint, personality.cruiseSpeed);
    }

    // ================= the number game =================

    void ChestGame(float dt)
    {
        if (chest == null) { SetState(State.Wander); return; }
        if (searchChest != chest) ResetSearch(chest);
        Vector3 cp = chest.transform.position;
        float d = Dist(cp);

        switch (phase)
        {
            case ChestPhase.Approach:
                if (odo.IsLocked) { CheckNumber(); break; }                     // already locked: free hint
                if (d < checkDistance && driver.Travelled - unlockTravelMark > 6f) { phase = ChestPhase.Stopping; phaseTimer = 0f; break; }
                driver.GoTo(OrbitPoint(cp), 1f);
                MaybeTurbo(0.3f);
                break;

            case ChestPhase.Stopping:
                driver.StopCar();
                phaseTimer += dt;
                if (odo.IsLocked) CheckNumber();
                else if (driver.IsStopped && phaseTimer > 1.2f) { odo.Lock(); CheckNumber(); }
                break;

            case ChestPhase.Thinking:
                driver.StopCar();
                phaseTimer -= dt;
                if (phaseTimer <= 0f) StartAdjust(false);
                break;

            case ChestPhase.Adjusting:
            {
                if (odo.IsLocked) { CheckNumber(); break; }                     // stopped by accident: use it
                float remaining = adjustGoal - (driver.Travelled - adjustStart);
                if (remaining <= BrakingDistance() + misjudge) { phase = ChestPhase.Stopping; phaseTimer = 0f; driver.StopCar(); break; }
                driver.GoTo(OrbitPoint(cp), remaining < 15f ? 0.6f : 1f);
                break;
            }

            case ChestPhase.Reversing:
            {
                if (odo.IsLocked) { CheckNumber(); break; }
                float remaining = (driver.Travelled - adjustStart) - adjustGoal; // adjustGoal is negative
                if (driver.IsUnsticking) { StartAdjust(true); break; }          // blocked behind: count forward instead
                if (remaining <= BrakingDistance() + misjudge) { phase = ChestPhase.Stopping; phaseTimer = 0f; driver.StopCar(); break; }
                driver.ReverseStraight(remaining < 6f ? 0.5f : 0.9f);
                break;
            }

            case ChestPhase.Ramming:
                if (!odo.IsLocked) { phase = ChestPhase.Approach; unlockTravelMark = driver.Travelled; break; } // a cat scrambled us
                driver.Chase(cp, d < 10f ? 0.8f : 1f);
                if (d > 12f) MaybeTurbo(0.5f);
                break;
        }
    }

    void ResetSearch(Chest c)
    {
        searchChest = c;
        lo = 1; hi = 99;
        phase = ChestPhase.Approach;
        unlockTravelMark = driver.Travelled;
        if (odo.IsLocked && Random.value < 0.5f) odo.Unlock();
    }

    void CheckNumber()
    {
        var hint = HintResult.Evaluate(odo.Number, chest.Number);
        Log($"check {odo.Number} vs chest -> {hint.Text} (range {lo}-{hi})");
        if (hint.OpensChest) { phase = ChestPhase.Ramming; return; }

        int n = odo.Number;
        bool readsWay = hint.way && Random.value < 0.4f + personality.numberSkill * 0.6f;
        if (hint.kind == HintKind.TooHigh) hi = Mathf.Min(hi, n - (readsWay ? HintResult.WayRange + 1 : HintResult.CloseRange + 1));
        else lo = Mathf.Max(lo, n + (readsWay ? HintResult.WayRange + 1 : HintResult.CloseRange + 1));
        if (lo > hi) { lo = 1; hi = 99; }

        phase = ChestPhase.Thinking;
        phaseTimer = Random.Range(personality.hintPause.x, personality.hintPause.y);
    }

    void StartAdjust(bool forceForward)
    {
        int n = odo.Number;
        int desired = (lo + hi) / 2;
        if (Random.value > personality.numberSkill) desired += Random.Range(-10, 11);  // a sloppy guess
        desired = Mathf.Clamp(desired, 1, 99);
        if (desired == n) desired = Odometer.Wrap(n + (Random.value < 0.5f ? 3 : -3));
        int delta = desired - n;

        Log($"aim for {desired} (from {n}, range {lo}-{hi})");
        odo.Unlock();
        adjustStart = driver.Travelled;
        misjudge = (1f - personality.numberSkill) * Random.Range(-3f, 3f);
        if (delta < 0 && -delta <= 20 && !forceForward)
        {
            phase = ChestPhase.Reversing;
            adjustGoal = delta * 3f - 1.5f;
        }
        else
        {
            int steps = delta > 0 ? delta : delta + 99;   // counting forward wraps 99 -> 1
            phase = ChestPhase.Adjusting;
            adjustGoal = steps * 3f + 1.5f;
        }
    }

    void OnHint(HintResult h)
    {
        // A chest bounced us (number wasn't close enough): treat it like a check.
        if (Current == State.SeekChest && phase == ChestPhase.Ramming && chest != null && !h.OpensChest)
        {
            if (!odo.IsLocked) odo.Lock();
            CheckNumber();
        }
    }

    float BrakingDistance()
    {
        float v = Mathf.Abs(driver.Speed);
        return v * v / 100f + 0.4f;
    }

    Vector3 OrbitPoint(Vector3 centre)
    {
        orbitAngle += Time.deltaTime * 25f;
        float a = orbitAngle * Mathf.Deg2Rad;
        Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * chestOrbitRadius;
        if (Dist(centre) > chestOrbitRadius * 2f) return centre; // far away: just head there
        return p;
    }

    // ================= helpers =================

    void MaybeTurbo(float eagernessScale)
    {
        if (turbo == null || !turbo.IsReady) return;
        float chance = personality.turboEagerness * eagernessScale * Time.deltaTime;
        if (personality.chaos > 0f) chance += personality.chaos * 0.2f * Time.deltaTime;
        if (Random.value < chance) turbo.TryActivate();
    }

    Vector3 PickFleePoint()
    {
        if (threat == null || roadPoints.Count == 0) return RandomPoint(roadPoints);
        Vector3 best = transform.position;
        float bestScore = float.MinValue;
        for (int i = 0; i < 16; i++)
        {
            Vector3 p = roadPoints[Random.Range(0, roadPoints.Count)];
            float score = Vector3.Distance(p, threat.transform.position) - Dist(p) * 0.4f;
            if (Physics.Linecast(threat.transform.position + Vector3.up * 2f, p + Vector3.up * 1.5f, losMask, QueryTriggerInteraction.Ignore)) score += 15f; // hidden behind buildings
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    CarHealth NearestEnemy()
    {
        CarHealth best = null;
        float bestD = float.MaxValue;
        foreach (var other in CarHealth.Alive)
        {
            if (other == health) continue;
            float d = Dist(other.transform.position);
            if (d < bestD) { bestD = d; best = other; }
        }
        return best;
    }

    bool CanSee(CarHealth other) =>
        !Physics.Linecast(transform.position + Vector3.up * 2f, other.transform.position + Vector3.up * 1.5f, losMask, QueryTriggerInteraction.Ignore);

    float Dist(Vector3 p) { p -= transform.position; p.y = 0f; return p.magnitude; }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>A random point from the list that's safe from the storm.</summary>
    Vector3 RandomPoint(List<Vector3> list)
    {
        var storm = StormController.Instance;
        for (int i = 0; i < 12 && list.Count > 0; i++)
        {
            var p = list[Random.Range(0, list.Count)];
            if (storm == null || storm.IsInsideTarget(p, 4f)) return p;
        }
        return storm != null ? storm.ClosestSafePoint(transform.position, 10f) : transform.position;
    }

    static Vector3 NearestTo(List<Vector3> list, Vector3 to, int skip)
    {
        if (list.Count == 0) return to;
        var sorted = new List<Vector3>(list);
        sorted.Sort((a, b) => (a - to).sqrMagnitude.CompareTo((b - to).sqrMagnitude));
        return sorted[Mathf.Min(skip + Random.Range(0, 2), sorted.Count - 1)];
    }

    /// <summary>Road, open-ground and street-corner points from the arena grid (shared by all AI).</summary>
    static void EnsurePoints()
    {
        if (pointsGrid != null) return;
        pointsGrid = FindAnyObjectByType<ArenaGrid>();
        roadPoints.Clear(); openPoints.Clear(); cornerPoints.Clear();
        if (pointsGrid == null) return;
        for (int y = 0; y < pointsGrid.Height; y++)
        for (int x = 0; x < pointsGrid.Width; x++)
        {
            char c = pointsGrid.Get(x, y);
            Vector3 p = pointsGrid.TileCenter(x, y);
            if (c == 'R')
            {
                roadPoints.Add(p);
                int n = 0;
                if (pointsGrid.Get(x + 1, y) == 'R') n++;
                if (pointsGrid.Get(x - 1, y) == 'R') n++;
                if (pointsGrid.Get(x, y + 1) == 'R') n++;
                if (pointsGrid.Get(x, y - 1) == 'R') n++;
                if (n >= 3) cornerPoints.Add(p);
            }
            else if (c == 'P' || c == 'K') openPoints.Add(p);
        }
    }

    void OnDestroy() { if (pointsGrid != null && pointsGrid.gameObject.scene != gameObject.scene) pointsGrid = null; }

    /// <summary>Clear the shared point cache (scene reload).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { pointsGrid = null; }

    public void SetPersonality(AIPersonality p) => personality = p;
}
