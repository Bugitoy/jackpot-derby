using UnityEngine;

/// <summary>
/// Anything that can drive a car: the keyboard (player) or an AI brain.
/// CarMotor reads these values every physics step, so player and AI cars obey the same physics.
/// </summary>
public abstract class CarInputSource : MonoBehaviour
{
    /// <summary>-1 (brake / reverse) to 1 (accelerate).</summary>
    public abstract float Throttle { get; }

    /// <summary>-1 (left) to 1 (right).</summary>
    public abstract float Steer { get; }
}
