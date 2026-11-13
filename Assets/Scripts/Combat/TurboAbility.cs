using System;
using UnityEngine;

/// <summary>Turbo (Shift): 2 s of +40% speed and +50% fire rate, then a 6 s cooldown. Speed lines while active.</summary>
[RequireComponent(typeof(CarMotor))]
public class TurboAbility : MonoBehaviour
{
    [SerializeField] float duration = 2f;
    [SerializeField] float cooldown = 6f;
    [SerializeField] float speedMultiplier = 1.4f;
    [SerializeField] float fireRateMultiplier = 1.5f;
    [Tooltip("Instant shove forward when Turbo kicks in.")]
    [SerializeField] float kick = 6f;

    public float ActiveLeft { get; private set; }
    public float CooldownLeft { get; private set; }
    public bool IsActive => ActiveLeft > 0f;
    public bool IsReady => !IsActive && CooldownLeft <= 0f;
    /// <summary>0 = just used, 1 = ready.</summary>
    public float Readiness => IsActive ? 0f : 1f - Mathf.Clamp01(CooldownLeft / cooldown);

    public event Action Activated;

    CarMotor motor;
    AutoBlaster blaster;
    CarHealth health;
    ParticleSystem lines;

    void Awake()
    {
        motor = GetComponent<CarMotor>();
        blaster = GetComponent<AutoBlaster>();
        health = GetComponent<CarHealth>();
    }

    public bool TryActivate()
    {
        if (!IsReady || (health != null && !health.IsAlive) || !motor.InputEnabled) return false;
        ActiveLeft = duration;
        motor.SpeedMultiplier = speedMultiplier;
        if (blaster != null) blaster.FireRateMultiplier = fireRateMultiplier;
        motor.Body.AddForce(transform.forward * kick, ForceMode.VelocityChange);
        if (lines == null && FxLibrary.I != null) lines = FxLibrary.I.CreateSpeedLines(transform);
        if (lines != null) lines.Play();
        Activated?.Invoke();
        return true;
    }

    void Update()
    {
        if (IsActive)
        {
            ActiveLeft -= Time.deltaTime;
            if (ActiveLeft <= 0f || (health != null && !health.IsAlive)) End();
        }
        else if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - Time.deltaTime);
    }

    void End()
    {
        ActiveLeft = 0f;
        CooldownLeft = cooldown;
        motor.SpeedMultiplier = 1f;
        if (blaster != null) blaster.FireRateMultiplier = 1f;
        if (lines != null) lines.Stop();
    }
}
