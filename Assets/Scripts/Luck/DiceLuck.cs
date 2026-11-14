using System;
using UnityEngine;

/// <summary>
/// Dice luck: every dice a car grabs adds a stack (max 5), and each stack raises the odds that the dice
/// rolls the nearest chest's exact number. A miss still lands near it, closer with every stack.
/// The rolled number locks, so you can drive straight to the chest and ram it.
/// Opening a chest spends the luck (stacks reset); a black cat wipes it too.
/// </summary>
public class DiceLuck : MonoBehaviour
{
    public const int MaxStacks = 5;

    [Tooltip("Chance of rolling the exact number with 1, 2, 3, 4, 5 dice.")]
    [SerializeField] float[] exactChance = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };
    [Tooltip("On a miss, how far off the roll can land with 1, 2, 3, 4, 5 dice.")]
    [SerializeField] int[] missSpread = { 20, 12, 8, 5, 5 };
    [Tooltip("How long the reels spin before the result is revealed.")]
    [SerializeField] float spinTime = 1.0f;

    public int Stacks { get; private set; }
    /// <summary>Chance of the exact number on the next dice (counting the dice you're about to grab).</summary>
    public float NextChance => ChanceFor(Mathf.Min(MaxStacks, Stacks + 1));
    public float ChanceFor(int stacks) => stacks <= 0 ? 0f : exactChance[Mathf.Clamp(stacks, 1, exactChance.Length) - 1];
    public float SpinTime => spinTime;

    public event Action<int> StacksChanged;
    /// <summary>A roll started: (stacks used, had a chest to aim at). The reels spin for SpinTime.</summary>
    public event Action<int, bool> RollStarted;
    /// <summary>The reels landed: (hint against the chest, was it the exact number, stacks used).</summary>
    public event Action<HintResult, bool, int> RollLanded;

    Odometer odo;
    float revealAt = -1f;
    HintResult pendingHint;
    bool pendingExact;
    int pendingStacks, pendingNumber;
    public bool IsSpinning => revealAt >= 0f;

    public static DiceLuck For(GameObject car)
    {
        var d = car.GetComponent<DiceLuck>();
        return d != null ? d : car.AddComponent<DiceLuck>();
    }

    void Awake() => odo = GetComponent<Odometer>();

    void OnEnable()
    {
        Chest.AnyOpened += OnChestOpened;
        BlackCat.AnyScrambled += OnCat;
    }

    void OnDisable()
    {
        Chest.AnyOpened -= OnChestOpened;
        BlackCat.AnyScrambled -= OnCat;
    }

    void OnChestOpened(Chest c, GameObject car, HintResult h) { if (car == gameObject) { SetStacks(0); EndSpin(); } }
    void OnCat(GameObject car, int n) { if (car == gameObject) { SetStacks(0); EndSpin(); } }

    void EndSpin()
    {
        revealAt = -1f;
        if (odo != null) odo.HoldHints = false;
    }

    void SetStacks(int n)
    {
        n = Mathf.Clamp(n, 0, MaxStacks);
        if (n == Stacks) return;
        Stacks = n;
        StacksChanged?.Invoke(n);
    }

    /// <summary>Called by the dice pickup. Adds a stack and rolls. Returns the popup line.</summary>
    public string Roll()
    {
        if (odo == null) odo = GetComponent<Odometer>();
        if (odo == null) return "";
        SetStacks(Stacks + 1);
        int stacks = Stacks;

        var chest = Chest.Nearest(transform.position);
        if (chest == null)
        {
            // Nothing to aim at yet: an ordinary random roll, but the luck is banked.
            odo.SetNumber(UnityEngine.Random.Range(1, 100), true);
            RollStarted?.Invoke(stacks, false);
            return "no chest yet - luck saved for later";
        }

        int target = chest.Number;
        pendingExact = UnityEngine.Random.value < ChanceFor(stacks);
        int n = target;
        if (!pendingExact)
        {
            int spread = missSpread[Mathf.Clamp(stacks, 1, missSpread.Length) - 1];
            int off = UnityEngine.Random.Range(1, spread + 1) * (UnityEngine.Random.value < 0.5f ? -1 : 1);
            n = target + off;
            if (n < 1 || n > 99) n = target - off;   // stay on the near side, no wrapping round
        }
        odo.SetNumber(n, false);
        odo.Lock();
        pendingHint = HintResult.Evaluate(n, target);
        pendingStacks = stacks;
        pendingNumber = n;
        revealAt = Time.time + spinTime;
        odo.HoldHints = true;   // stopping mid-spin mustn't spoil the result early
        RollStarted?.Invoke(stacks, true);
        return stacks >= MaxStacks ? "LUCKY NUMBER GUARANTEED!" : $"{Mathf.RoundToInt(ChanceFor(stacks) * 100f)}% chance of the LUCKY number";
    }

    void Update()
    {
        if (revealAt < 0f || Time.time < revealAt) return;
        EndSpin();
        // R, a chest or a cat may have changed the number while the reels spun: only reveal the real result.
        if (odo == null || !odo.IsLocked || odo.Number != pendingNumber) return;
        odo.RevealHint(pendingHint);
        RollLanded?.Invoke(pendingHint, pendingExact, pendingStacks);
    }
}
