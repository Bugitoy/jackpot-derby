using UnityEngine;

public enum DamageKind { Blaster, Ram, RamRecoil, Blast, Storm, Other }

/// <summary>Everything about one hit, passed to HUD, effects and the match manager.</summary>
public struct DamageInfo
{
    public CarHealth victim;
    public CarIdentity attacker;
    public float amount;
    /// <summary>How much of the hit the blaster bar soaked up.</summary>
    public float toBlaster;
    public float toHealth;
    public DamageKind kind;
    public Vector3 point;
}
