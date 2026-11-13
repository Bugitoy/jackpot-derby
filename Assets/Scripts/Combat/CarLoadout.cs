using System;
using UnityEngine;

public enum BlasterTier { None, Small, Jackpot }

/// <summary>
/// Which blaster the car carries. A blaster is kept until the car is destroyed (no blaster bar, no
/// demotion). Chests call GrantFromChest. Shows the matching blaster model on the roof.
/// </summary>
public class CarLoadout : MonoBehaviour
{
    [Header("Roof visuals")]
    [SerializeField] GameObject smallBlasterVisual;
    [SerializeField] GameObject jackpotBlasterVisual;
    [SerializeField] Transform mount;
    [Tooltip("Roof rack + luggage parts of the car model. Shown when unarmed, hidden while a blaster is mounted.")]
    [SerializeField] GameObject[] roofCargo;

    public BlasterTier Tier { get; private set; }
    public Transform Mount => mount;
    public bool IsArmed => Tier != BlasterTier.None;

    public event Action<BlasterTier> TierChanged;
    /// <summary>Popup text for a chest reward: (headline e.g. "SMALL BLASTER!", colour, small line underneath).</summary>
    public event Action<string, Color, string> RewardMessage;

    static readonly Color Teal = new Color(0.30f, 0.95f, 0.80f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.15f);
    static readonly Color Green = new Color(0.35f, 0.95f, 0.40f);

    void Awake() => RefreshVisuals();

    /// <summary>
    /// Opening a chest moves you up exactly one step: no blaster → small blaster → Jackpot cannon
    /// (the cannon is only ever an upgrade of the small blaster). Already on the cannon: full repair.
    /// Bonus: an exact number (LUCKY!) or a Jackpot chest also fully repairs the car.
    /// </summary>
    public void GrantFromChest(bool exact, bool jackpotChest)
    {
        var health = GetComponent<CarHealth>();
        string msg, sub = "";
        Color col;
        bool repaired = false;
        switch (Tier)
        {
            case BlasterTier.None:
                SetTier(BlasterTier.Small);
                msg = "SMALL BLASTER!"; col = Teal;
                break;
            case BlasterTier.Small:
                SetTier(BlasterTier.Jackpot);
                msg = "UPGRADE! JACKPOT CANNON"; col = Gold;
                break;
            default:
                if (health != null) health.RepairFull();
                repaired = true;
                msg = "FULL REPAIR!"; col = Green;
                break;
        }

        if ((exact || jackpotChest) && !repaired)
        {
            if (health != null) health.RepairFull();
            sub = "+ FULL REPAIR";
        }
        RewardMessage?.Invoke(msg, col, sub);
    }

    public void SetTier(BlasterTier tier)
    {
        Tier = tier;
        RefreshVisuals();
        TierChanged?.Invoke(tier);
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
