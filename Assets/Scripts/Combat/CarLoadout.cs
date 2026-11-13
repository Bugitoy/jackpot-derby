using System;
using UnityEngine;

public enum BlasterTier { None, Small, Jackpot }

/// <summary>
/// Which blaster the car carries and how full its blaster bar is. Chests call GrantFromChest.
/// Shows the matching blaster model on the roof.
/// </summary>
public class CarLoadout : MonoBehaviour
{
    [SerializeField] float smallBarMax = 50f;
    [SerializeField] float jackpotBarMax = 80f;
    [Tooltip("Blaster bar you keep when demoted from Jackpot cannon to small blaster.")]
    [SerializeField] float demotedSmallBar = 30f;

    [Header("Roof visuals")]
    [SerializeField] GameObject smallBlasterVisual;
    [SerializeField] GameObject jackpotBlasterVisual;
    [SerializeField] Transform mount;
    [Tooltip("Roof rack + luggage parts of the car model. Shown when unarmed, hidden while a blaster is mounted.")]
    [SerializeField] GameObject[] roofCargo;

    public BlasterTier Tier { get; private set; }
    public float BlasterBar { get; private set; }
    public float BlasterBarMax => Tier == BlasterTier.Jackpot ? jackpotBarMax : Tier == BlasterTier.Small ? smallBarMax : 0f;
    public Transform Mount => mount;
    public bool IsArmed => Tier != BlasterTier.None;

    public event Action<BlasterTier> TierChanged;
    /// <summary>Popup text for a chest reward: (headline e.g. "SMALL BLASTER!", colour, small line underneath).</summary>
    public event Action<string, Color, string> RewardMessage;

    static readonly Color Teal = new Color(0.30f, 0.95f, 0.80f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.15f);

    void Awake() => RefreshVisuals();

    /// <summary>
    /// Opening a chest always moves you up exactly one step: no blaster → small blaster → Jackpot cannon
    /// (the cannon is only ever an upgrade of the small blaster). Already on the cannon: refill its bar.
    /// Bonus: an exact number (LUCKY!) or a Jackpot chest also fully repairs the car.
    /// </summary>
    public void GrantFromChest(bool exact, bool jackpotChest)
    {
        string msg;
        Color col;
        switch (Tier)
        {
            case BlasterTier.None:
                SetTier(BlasterTier.Small, smallBarMax);
                msg = "SMALL BLASTER!"; col = Teal;
                break;
            case BlasterTier.Small:
                SetTier(BlasterTier.Jackpot, jackpotBarMax);
                msg = "UPGRADE! JACKPOT CANNON"; col = Gold;
                break;
            default:
                BlasterBar = jackpotBarMax;
                TierChanged?.Invoke(Tier);
                msg = "CANNON REFILLED"; col = Gold;
                break;
        }

        string sub = "";
        if (exact || jackpotChest)
        {
            var health = GetComponent<CarHealth>();
            if (health != null) health.RepairFull();
            sub = "+ FULL REPAIR";
        }
        RewardMessage?.Invoke(msg, col, sub);
    }

    public void SetTier(BlasterTier tier, float bar)
    {
        Tier = tier;
        BlasterBar = Mathf.Clamp(bar, 0f, BlasterBarMax);
        RefreshVisuals();
        TierChanged?.Invoke(tier);
    }

    /// <summary>Fired when the blaster bar runs out and the car is demoted ("BUSTED").</summary>
    public event Action<BlasterTier> Busted;

    /// <summary>Soak up damage with the blaster bar. Returns how much it absorbed.</summary>
    public float AbsorbDamage(float amount)
    {
        if (!IsArmed || BlasterBar <= 0f) return 0f;
        float used = Mathf.Min(amount, BlasterBar);
        BlasterBar -= used;
        if (BlasterBar <= 0.01f)
        {
            Demote();
            Busted?.Invoke(Tier);
        }
        return used;
    }

    /// <summary>Blaster bar emptied: Jackpot → small (30 bar), small → none.</summary>
    public void Demote()
    {
        if (Tier == BlasterTier.Jackpot) SetTier(BlasterTier.Small, demotedSmallBar);
        else SetTier(BlasterTier.None, 0f);
    }

    void RefreshVisuals()
    {
        if (smallBlasterVisual != null) smallBlasterVisual.SetActive(Tier == BlasterTier.Small);
        if (jackpotBlasterVisual != null) jackpotBlasterVisual.SetActive(Tier == BlasterTier.Jackpot);
        if (roofCargo != null)
            foreach (var part in roofCargo)
                if (part != null) part.SetActive(!IsArmed);
    }
}
