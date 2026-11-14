using System;
using UnityEngine;

/// <summary>
/// The car's lucky number (1–99, wrapping). Driving forward counts up 1 per 3 units, reversing counts
/// down. Stopping (after having moved) locks the number and reveals a hint against the nearest chest.
/// A locked number stays locked while you drive; Unlock() (R key, chest, dice, cat) lets it roll again.
/// </summary>
[RequireComponent(typeof(CarMotor))]
public class Odometer : MonoBehaviour
{
    [SerializeField] float unitsPerNumber = 3f;
    [Tooltip("Below this speed the car counts as stopped.")]
    [SerializeField] float stopSpeed = 0.5f;
    [Tooltip("How long you must stay stopped to lock.")]
    [SerializeField] float stopTime = 0.4f;
    [Tooltip("You must go faster than this before a stop counts again (so sitting still doesn't spam).")]
    [SerializeField] float armSpeed = 2f;

    public int Number { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsStopped => stoppedTimer >= stopTime;
    /// <summary>How far toward the next number we are (-1..1). Used to roll the reels smoothly.</summary>
    public float Progress => IsLocked ? 0f : distance / unitsPerNumber;
    public HintResult LastHint { get; private set; } = HintResult.NoChest;
    /// <summary>While the dice reels spin, stopping locks but doesn't reveal a hint (DiceLuck reveals it).</summary>
    public bool HoldHints { get; set; }

    public event Action<int> NumberChanged;
    public event Action<bool> LockChanged;
    public event Action<HintResult> HintRevealed;

    CarMotor motor;
    float distance;
    float stoppedTimer;
    bool armed;

    void Awake()
    {
        motor = GetComponent<CarMotor>();
        Number = UnityEngine.Random.Range(1, 100);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float speed = motor.ForwardSpeed;

        if (Mathf.Abs(speed) > armSpeed) armed = true;
        if (Mathf.Abs(speed) < stopSpeed)
        {
            stoppedTimer += dt;
            if (armed && stoppedTimer >= stopTime) { armed = false; OnStopped(); }
        }
        else stoppedTimer = 0f;

        if (IsLocked || !motor.IsGrounded) return;
        distance += speed * dt;
        while (distance >= unitsPerNumber) { distance -= unitsPerNumber; SetNumberInternal(Wrap(Number + 1)); }
        while (distance <= -unitsPerNumber) { distance += unitsPerNumber; SetNumberInternal(Wrap(Number - 1)); }
    }

    public static int Wrap(int n) => ((n - 1) % 99 + 99) % 99 + 1;

    void OnStopped()
    {
        if (!IsLocked) Lock();
        if (!HoldHints) RevealHintAgainstNearestChest();
    }

    public void Lock()
    {
        if (IsLocked) return;
        IsLocked = true;
        distance = 0f;
        LockChanged?.Invoke(true);
    }

    public void Unlock()
    {
        if (!IsLocked) return;
        IsLocked = false;
        distance = 0f;
        LockChanged?.Invoke(false);
    }

    /// <summary>Set the number directly (dice, black cat, cheats).</summary>
    public void SetNumber(int n, bool unlock)
    {
        SetNumberInternal(Wrap(n));
        if (unlock) Unlock();
    }

    /// <summary>Black cat: shift the number by a random amount and unlock.</summary>
    public void Scramble(int maxShift)
    {
        int shift = 0;
        while (shift == 0) shift = UnityEngine.Random.Range(-maxShift, maxShift + 1);
        SetNumber(Number + shift, true);
    }

    public void RevealHintAgainstNearestChest()
    {
        var chest = Chest.Nearest(transform.position);
        RevealHint(chest != null ? HintResult.Evaluate(Number, chest.Number) : HintResult.NoChest);
    }

    public void RevealHint(HintResult hint)
    {
        LastHint = hint;
        HintRevealed?.Invoke(hint);
    }

    void SetNumberInternal(int n)
    {
        if (n == Number) return;
        Number = n;
        NumberChanged?.Invoke(n);
    }
}
