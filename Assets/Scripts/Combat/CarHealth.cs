using System;
using UnityEngine;

/// <summary>Car health (100). Damage, the blaster bar soaking hits, and elimination come in Milestone 4.</summary>
public class CarHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => Health > 0f;

    public event Action<float> HealthChanged;

    void Awake() => Health = maxHealth;

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        Health = Mathf.Min(maxHealth, Health + amount);
        HealthChanged?.Invoke(Health);
    }

    public void RepairFull() => Heal(maxHealth);
}
