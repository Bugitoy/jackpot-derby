using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A chest with a hidden lucky number. Ram it: exact or within ±5 opens it and upgrades your blaster one step;
/// too far off bounces you away (no damage) and the chest stays. Every attempt shows the hint.
/// </summary>
public class Chest : MonoBehaviour
{
    public static readonly List<Chest> Active = new List<Chest>();

    [SerializeField] bool isJackpot;
    [Tooltip("Minimum impact speed that counts as ramming.")]
    [SerializeField] float minRamSpeed = 2f;
    [Tooltip("Speed you're knocked back with on a miss.")]
    [SerializeField] float bounceSpeed = 14f;
    [Tooltip("Seconds before the same car can try again.")]
    [SerializeField] float retryCooldown = 0.6f;

    [Header("Visuals")]
    [SerializeField] Transform visual;
    [Tooltip("Pillar of light, ground rings, floating token and particles (see ChestFx).")]
    [SerializeField] ChestFx fx;
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float spinSpeed = 18f;
    [Tooltip("Every this many seconds the chest gives an eager little hop.")]
    [SerializeField] float hopEvery = 3.2f;

    public int Number { get; private set; }
    public bool IsJackpot => isJackpot;

    /// <summary>(chest, car that opened it, the hint at that moment)</summary>
    public static event Action<Chest, GameObject, HintResult> AnyOpened;
    public static event Action<Chest> AnySpawned;

    // This project enters Play mode without reloading scripts, so static events and lists would keep
    // the previous session's destroyed objects (whose handlers then throw). Start every session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AnyOpened = null; AnySpawned = null; Active.Clear(); }

    readonly Dictionary<CarMotor, float> lastTry = new Dictionary<CarMotor, float>();
    float age;
    float wobble;
    Vector3 visualBase;
    float openAge = -1f;
    float hopOffset;

    void Awake()
    {
        Number = UnityEngine.Random.Range(Odometer.Min, Odometer.Max + 1);
        if (visual != null) visualBase = visual.localPosition;
        hopOffset = UnityEngine.Random.Range(0f, hopEvery);
    }

    void OnEnable() { Active.Add(this); AnySpawned?.Invoke(this); }
    void OnDisable() => Active.Remove(this);

    public static Chest Nearest(Vector3 p)
    {
        Chest best = null;
        float bestSqr = float.MaxValue;
        foreach (var c in Active)
        {
            float d = (c.transform.position - p).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = c; }
        }
        return best;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        wobble = Mathf.Max(0f, wobble - dt * 3f);
        if (visual == null) return;

        if (openAge >= 0f)
        {
            // Opened: a fat pop, then it spins down to nothing inside the burst of light.
            openAge += dt;
            float k = openAge;
            float s = k < 0.12f ? Mathf.Lerp(1f, 1.3f, k / 0.12f) : Mathf.Lerp(1.3f, 0f, Mathf.Clamp01((k - 0.12f) / 0.33f));
            visual.localScale = new Vector3(s, s * (k < 0.12f ? 0.85f : 1.1f), s);
            visual.localRotation = Quaternion.Euler(0f, age * spinSpeed + k * 720f, 0f);
            visual.localPosition = visualBase + Vector3.up * k * 2f;
            return;
        }

        // Spawn: drops in from above and lands with a squash.
        float drop = Mathf.Clamp01(age / 0.45f);
        float fall = (1f - drop * drop) * 7f;
        float land = age > 0.45f && age < 0.75f ? Mathf.Sin((age - 0.45f) / 0.3f * Mathf.PI) : 0f;

        // Idle: a gentle bob and turn, and every few seconds an eager hop with squash and stretch.
        float hopT = (age + hopOffset) % hopEvery;
        float hop = hopT < 0.42f ? Mathf.Sin(hopT / 0.42f * Mathf.PI) : 0f;
        float squash = hopT < 0.08f ? -0.12f * Mathf.Sin(hopT / 0.08f * Mathf.PI) : hop * 0.08f;
        float shake = wobble > 0f ? Mathf.Sin(age * 60f) * 12f * wobble : 0f;

        visual.localPosition = visualBase + Vector3.up * (fall + (Mathf.Sin(age * 2.2f) * 0.5f + 0.5f) * bobHeight + hop * 0.45f);
        visual.localRotation = Quaternion.Euler(hop * 6f, age * spinSpeed, shake + hop * 4f);
        float sy = 1f + squash - land * 0.22f, sxz = 1f - squash * 0.6f + land * 0.14f;
        visual.localScale = new Vector3(sxz, sy, sxz);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (openAge >= 0f || collision.rigidbody == null) return;
        var motor = collision.rigidbody.GetComponent<CarMotor>();
        if (motor == null || collision.relativeVelocity.magnitude < minRamSpeed) return;
        TryOpen(motor);
    }

    /// <summary>
    /// Cracked open: it leaves play at once (no more chest markers, no AI interest, no collisions) and
    /// plays its burst - the chest pops and spins away, the light flares out - before it is removed.
    /// </summary>
    void Open()
    {
        openAge = 0f;
        Active.Remove(this);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (fx != null) fx.PlayOpen();
        if (FxLibrary.I != null)
        {
            FxLibrary.I.CoinBurst(transform.position + Vector3.up, isJackpot ? 26 : 16);
            FxLibrary.I.BlastRing(transform.position + Vector3.up * 0.2f, 6f, fx != null ? fx.MainColor : Color.yellow);
            FxLibrary.I.HitPuff(transform.position + Vector3.up * 1.2f, Color.white, 2.2f);
        }
        Destroy(gameObject, 1.1f);
    }

    void TryOpen(CarMotor car)
    {
        if (lastTry.TryGetValue(car, out float t) && Time.time - t < retryCooldown) return;
        lastTry[car] = Time.time;

        var odometer = car.GetComponent<Odometer>();
        var loadout = car.GetComponent<CarLoadout>();
        if (odometer == null || loadout == null) return;

        var hint = HintResult.Evaluate(odometer.Number, Number);
        odometer.RevealHint(hint);

        if (hint.OpensChest)
        {
            loadout.GrantFromChest(hint.kind == HintKind.Lucky, isJackpot);
            odometer.Unlock();
            AnyOpened?.Invoke(this, car.gameObject, hint);
            Open();
        }
        else
        {
            Vector3 away = car.transform.position - transform.position;
            away.y = 0f;
            car.ApplyKnockback(away.normalized * bounceSpeed, 0.45f);
            var who = car.GetComponent<CarIdentity>();
            GameAudio.ChestBonk(transform.position, who != null && who.isPlayer);
            wobble = 1f;
        }
    }
}
