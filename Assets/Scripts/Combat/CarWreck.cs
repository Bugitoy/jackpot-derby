using UnityEngine;

/// <summary>When a car is eliminated it stops driving, loses its blaster and stays in the arena as a heavy wreck (cover!).</summary>
public class CarWreck : MonoBehaviour
{
    [SerializeField] float wreckMass = 4000f;
    [SerializeField] float wreckDrag = 3f;

    public bool IsWrecked { get; private set; }

    void Awake()
    {
        var health = GetComponent<CarHealth>();
        if (health != null) health.Eliminated += (h, killer) => Wreck();
    }

    void Wreck()
    {
        IsWrecked = true;
        var motor = GetComponent<CarMotor>();
        if (motor != null) { motor.InputEnabled = false; motor.SpeedMultiplier = 1f; }
        var loadout = GetComponent<CarLoadout>();
        if (loadout != null) loadout.SetTier(BlasterTier.None, 0f);
        var odometer = GetComponent<Odometer>();
        if (odometer != null) odometer.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = wreckMass;
            rb.linearDamping = wreckDrag;
            rb.AddForce(Vector3.up * 2f, ForceMode.VelocityChange);
        }
    }
}
