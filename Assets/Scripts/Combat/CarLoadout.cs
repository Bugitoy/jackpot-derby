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

    public BlasterTier Tier { get; private set; }
    public float BlasterBar { get; private set; }
    public float BlasterBarMax => Tier == BlasterTier.Jackpot ? jackpotBarMax : Tier == BlasterTier.Small ? smallBarMax : 0f;
    public Transform Mount => mount;
    public bool IsArmed => Tier != BlasterTier.None;

    public event Action<BlasterTier> TierChanged;
    /// <summary>A short message for the popup, e.g. "SMALL BLASTER!".</summary>
    public event Action<string, Color> RewardMessage;

    static readonly Color Teal = new Color(0.30f, 0.95f, 0.80f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.15f);

    void Awake() => RefreshVisuals();

    /// <summary>exact = your number matched exactly; jackpotChest = this was a Jackpot chest.</summary>
    public void GrantFromChest(bool exact, bool jackpotChest)
    {
        if (exact)
        {
            if (Tier == BlasterTier.Jackpot)
            {
                var health = GetComponent<CarHealth>();
                if (health != null) health.RepairFull();
                BlasterBar = jackpotBarMax;
                TierChanged?.Invoke(Tier);
                RewardMessage?.Invoke("FULL REPAIR + REFILL!", Gold);
            }
            else
            {
                SetTier(BlasterTier.Jackpot, jackpotBarMax);
                RewardMessage?.Invoke("JACKPOT CANNON!", Gold);
            }
            return;
        }

        // Close (within ±5).
        if (Tier == BlasterTier.Jackpot)
        {
            BlasterBar = jackpotBarMax;
            TierChanged?.Invoke(Tier);
            RewardMessage?.Invoke("BLASTER REFILLED", Gold);
        }
        else if (Tier == BlasterTier.Small || jackpotChest)
        {
            bool upgrade = Tier == BlasterTier.Small;
            SetTier(BlasterTier.Jackpot, jackpotBarMax);
            RewardMessage?.Invoke(upgrade ? "UPGRADE! JACKPOT CANNON" : "JACKPOT CANNON!", Gold);
        }
        else
        {
            SetTier(BlasterTier.Small, smallBarMax);
            RewardMessage?.Invoke("SMALL BLASTER!", Teal);
        }
    }

    public void SetTier(BlasterTier tier, float bar)
    {
        Tier = tier;
        BlasterBar = Mathf.Clamp(bar, 0f, BlasterBarMax);
        RefreshVisuals();
        TierChanged?.Invoke(tier);
    }

    /// <summary>Blaster bar emptied: Jackpot → small (30 bar), small → none. (Used in Milestone 4.)</summary>
    public void Demote()
    {
        if (Tier == BlasterTier.Jackpot) SetTier(BlasterTier.Small, demotedSmallBar);
        else SetTier(BlasterTier.None, 0f);
    }

    void RefreshVisuals()
    {
        if (smallBlasterVisual != null) smallBlasterVisual.SetActive(Tier == BlasterTier.Small);
        if (jackpotBlasterVisual != null) jackpotBlasterVisual.SetActive(Tier == BlasterTier.Jackpot);
    }
}
