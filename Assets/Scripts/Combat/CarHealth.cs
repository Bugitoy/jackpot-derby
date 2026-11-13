using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Car health (100). All damage comes straight off health; health 0 = destroyed (whatever blaster it had).
/// </summary>
public class CarHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => Health > 0f;
    public CarIdentity Identity { get; private set; }
    public CarIdentity LastAttacker { get; private set; }

    public event Action<DamageInfo> Damaged;
    public event Action<CarHealth, CarIdentity> Eliminated;
    public static event Action<DamageInfo> AnyDamaged;
    /// <summary>(victim, killer or null)</summary>
    public static event Action<CarHealth, CarIdentity> AnyEliminated;

    /// <summary>All cars still in the match.</summary>
    public static readonly List<CarHealth> Alive = new List<CarHealth>();

    CarLoadout loadout;

    void Awake()
    {
        Health = maxHealth;
        loadout = GetComponent<CarLoadout>();
        Identity = GetComponent<CarIdentity>();
    }

    void OnEnable() { if (IsAlive && !Alive.Contains(this)) Alive.Add(this); }
    void OnDisable() => Alive.Remove(this);

    public void TakeDamage(float amount, CarIdentity attacker, DamageKind kind, Vector3 point)
    {
        if (!IsAlive || amount <= 0f) return;
        if (attacker == Identity) attacker = null;

        float toBlaster = 0f;
        float toHealth = Mathf.Min(Health, amount);
        Health -= toHealth;
        if (attacker != null) LastAttacker = attacker;

        var info = new DamageInfo
        {
            victim = this, attacker = attacker, amount = amount, toBlaster = toBlaster,
            toHealth = toHealth, kind = kind, point = point
        };
        Damaged?.Invoke(info);
        AnyDamaged?.Invoke(info);

        if (Health <= 0f)
        {
            Health = 0f;
            Alive.Remove(this);
            Eliminated?.Invoke(this, LastAttacker);
            AnyEliminated?.Invoke(this, LastAttacker);
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        Health = Mathf.Min(maxHealth, Health + amount);
    }

    public void RepairFull() => Heal(maxHealth);
}
