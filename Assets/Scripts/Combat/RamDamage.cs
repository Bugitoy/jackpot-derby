using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Car-to-car crashes. The car driving harder into the other is the rammer.
/// Damage to the hit car = relative speed × 1.2 (× 1.5 on side or rear hits); the rammer takes 30%.
/// Below a small speed threshold there's no damage.
/// </summary>
[RequireComponent(typeof(CarMotor))]
public class RamDamage : MonoBehaviour
{
    [SerializeField] float damagePerSpeed = 1.2f;
    [SerializeField] float sideOrRearMultiplier = 1.5f;
    [SerializeField, Range(0f, 1f)] float rammerShare = 0.3f;
    [Tooltip("Relative speed below which crashes do no damage.")]
    [SerializeField] float minSpeed = 7f;
    [SerializeField] float knockbackPerSpeed = 0.55f;
    [SerializeField] float maxKnockback = 18f;
    [SerializeField] float pairCooldown = 0.6f;

    CarMotor motor;
    CarHealth health;
    CarIdentity identity;
    readonly Dictionary<RamDamage, float> lastHit = new Dictionary<RamDamage, float>();

    /// <summary>(victim, damage, was it a side/rear hit)</summary>
    public static event System.Action<RamDamage, RamDamage, float, bool> AnyRam;

    void Awake()
    {
        motor = GetComponent<CarMotor>();
        health = GetComponent<CarHealth>();
        identity = GetComponent<CarIdentity>();
    }

    void OnCollisionEnter(Collision c)
    {
        if (c.rigidbody == null) return;
        var other = c.rigidbody.GetComponent<RamDamage>();
        if (other == null || other == this) return;
        if (health == null || !health.IsAlive || other.health == null || !other.health.IsAlive) return;

        Vector3 myV = motor.LastVelocity, otherV = other.motor.LastVelocity;
        Vector3 toOther = other.transform.position - transform.position; toOther.y = 0f;
        if (toOther.sqrMagnitude < 1e-4f) return;
        toOther.Normalize();

        // Only the car that was driving harder into the other one handles the crash.
        float myPush = Vector3.Dot(myV, toOther);
        float otherPush = Vector3.Dot(otherV, -toOther);
        if (myPush < otherPush || (Mathf.Approximately(myPush, otherPush) && GetHashCode() < other.GetHashCode())) return;

        if (lastHit.TryGetValue(other, out float t) && Time.time - t < pairCooldown) return;
        lastHit[other] = Time.time;

        float rel = (myV - otherV).magnitude;
        if (rel < minSpeed) return;

        // Front of the victim facing us = head-on; otherwise it's a side or rear hit.
        Vector3 victimToMe = -toOther;
        bool sideOrRear = Vector3.Dot(Flat(other.transform.forward).normalized, victimToMe) < 0.7f;
        float damage = rel * damagePerSpeed * (sideOrRear ? sideOrRearMultiplier : 1f);
        Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : (transform.position + other.transform.position) * 0.5f;

        other.health.TakeDamage(damage, identity, DamageKind.Ram, point);
        health.TakeDamage(damage * rammerShare, other.identity, DamageKind.RamRecoil, point);
        other.motor.ApplyKnockback(toOther * Mathf.Min(rel * knockbackPerSpeed, maxKnockback), 0.35f);
        if (FxLibrary.I != null) { FxLibrary.I.HitPuff(point + Vector3.up, new Color(1f, 0.8f, 0.4f), 1.4f); FxLibrary.I.Dust(point, 1.3f); }
        AnyRam?.Invoke(this, other, damage, sideOrRear);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
