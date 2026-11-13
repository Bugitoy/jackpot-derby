using System;
using UnityEngine;

/// <summary>Jackpot Blast (Space, Jackpot cannon only): a big area blast in front of the car, 25 damage + knockback, 8 s cooldown.</summary>
[RequireComponent(typeof(CarLoadout))]
public class JackpotBlast : MonoBehaviour
{
    [SerializeField] float cooldown = 8f;
    [SerializeField] float damage = 25f;
    [SerializeField] float radius = 7f;
    [Tooltip("How far in front of the car the blast is centred.")]
    [SerializeField] float forwardOffset = 5f;
    [SerializeField] float knockback = 20f;

    public float CooldownLeft { get; private set; }
    public bool IsAvailable => loadout.Tier == BlasterTier.Jackpot && (health == null || health.IsAlive);
    public bool IsReady => IsAvailable && CooldownLeft <= 0f;
    public float Readiness => 1f - Mathf.Clamp01(CooldownLeft / cooldown);
    public float Radius => radius;

    /// <summary>(blast centre)</summary>
    public event Action<Vector3> Activated;

    CarLoadout loadout;
    CarHealth health;
    CarIdentity identity;
    CarMotor motor;

    void Awake()
    {
        loadout = GetComponent<CarLoadout>();
        health = GetComponent<CarHealth>();
        identity = GetComponent<CarIdentity>();
        motor = GetComponent<CarMotor>();
    }

    void Update()
    {
        if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - Time.deltaTime);
    }

    /// <summary>Where the blast would land right now.</summary>
    public Vector3 BlastCentre()
    {
        Vector3 f = transform.forward; f.y = 0f;
        return transform.position + f.normalized * forwardOffset;
    }

    public bool TryActivate()
    {
        if (!IsReady || (motor != null && !motor.InputEnabled)) return false;
        CooldownLeft = cooldown;
        Vector3 centre = BlastCentre();

        // Copy the list: damage can eliminate cars, which edits CarHealth.Alive.
        foreach (var car in CarHealth.Alive.ToArray())
        {
            if (car == health) continue;
            Vector3 d = car.transform.position - centre; d.y = 0f;
            if (d.magnitude > radius + 1f) continue;
            car.TakeDamage(damage, identity, DamageKind.Blast, car.transform.position + Vector3.up);
            var m = car.GetComponent<CarMotor>();
            Vector3 push = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
            if (m != null) m.ApplyKnockback(push * knockback, 0.5f);
        }
        if (FxLibrary.I != null)
        {
            FxLibrary.I.BlastRing(centre, radius, new Color(1f, 0.75f, 0.2f));
            FxLibrary.I.Explosion(centre, 0.8f);
        }
        Activated?.Invoke(centre);
        return true;
    }
}
