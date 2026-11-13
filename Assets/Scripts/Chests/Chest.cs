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
    [SerializeField] Renderer beam;
    [SerializeField] float bobHeight = 0.25f;
    [SerializeField] float spinSpeed = 25f;

    public int Number { get; private set; }
    public bool IsJackpot => isJackpot;

    /// <summary>(chest, car that opened it, the hint at that moment)</summary>
    public static event Action<Chest, GameObject, HintResult> AnyOpened;
    public static event Action<Chest> AnySpawned;

    readonly Dictionary<CarMotor, float> lastTry = new Dictionary<CarMotor, float>();
    float age;
    float wobble;
    Vector3 visualBase;
    MaterialPropertyBlock beamProps;
    Color beamColor;

    void Awake()
    {
        Number = UnityEngine.Random.Range(1, 100);
        if (visual != null) visualBase = visual.localPosition;
        if (beam != null)
        {
            beamProps = new MaterialPropertyBlock();
            beamColor = beam.sharedMaterial.GetColor("_BaseColor");
        }
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
        age += Time.deltaTime;
        wobble = Mathf.Max(0f, wobble - Time.deltaTime * 3f);
        if (visual != null)
        {
            float pop = age < 0.4f ? Mathf.Sin(age / 0.4f * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(age / 0.4f * Mathf.PI)) : 1f;
            float shake = wobble > 0f ? Mathf.Sin(age * 60f) * 12f * wobble : 0f;
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(age * 2.2f) * 0.5f + 0.5f) * bobHeight;
            visual.localRotation = Quaternion.Euler(0f, age * spinSpeed, shake);
            visual.localScale = Vector3.one * Mathf.Max(0.01f, pop);
        }
        if (beam != null)
        {
            if (beamProps == null) { beamProps = new MaterialPropertyBlock(); beamColor = beam.sharedMaterial.GetColor("_BaseColor"); }
            var c = beamColor;
            c.a *= 0.75f + 0.25f * Mathf.Sin(age * 3f);
            beamProps.SetColor("_BaseColor", c);
            beam.SetPropertyBlock(beamProps);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.rigidbody == null) return;
        var motor = collision.rigidbody.GetComponent<CarMotor>();
        if (motor == null || collision.relativeVelocity.magnitude < minRamSpeed) return;
        TryOpen(motor);
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
            Destroy(gameObject);
        }
        else
        {
            Vector3 away = car.transform.position - transform.position;
            away.y = 0f;
            car.ApplyKnockback(away.normalized * bounceSpeed, 0.45f);
            wobble = 1f;
        }
    }
}
