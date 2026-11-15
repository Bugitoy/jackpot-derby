using UnityEngine;

/// <summary>
/// Arcade car physics on a Rigidbody. The car can't flip (X/Z rotation frozen); we drive it by
/// steering its velocity directly, and keep a little sideways slide for a drifty feel.
/// Collisions still work because each step starts from whatever velocity physics left us with.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarMotor : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] float maxSpeed = 24f;
    [SerializeField] float maxReverseSpeed = 10f;
    [Tooltip("Acceleration from a standstill (units/s²). Fades a bit as you near top speed.")]
    [SerializeField] float acceleration = 26f;
    [SerializeField] float brakeDeceleration = 50f;
    [Tooltip("Slow-down when no throttle is held.")]
    [SerializeField] float coastDeceleration = 9f;

    [Header("Steering")]
    [Tooltip("Turn rate at full steer, degrees per second.")]
    [SerializeField] float turnRate = 160f;
    [Tooltip("Speed at which steering reaches full strength (cars can't spin on the spot).")]
    [SerializeField] float fullTurnSpeed = 5f;
    [Tooltip("Steering strength left at top speed (1 = same as low speed).")]
    [SerializeField, Range(0f, 1f)] float topSpeedTurnFactor = 0.7f;
    [Tooltip("How snappily the car's spin follows the steering.")]
    [SerializeField] float yawResponse = 14f;

    [Header("Grip")]
    [Tooltip("How fast sideways sliding is removed. Lower = driftier.")]
    [SerializeField] float grip = 6f;

    Rigidbody rb;
    CarInputSource input;
    float stunTimer;
    Vector3 lastGroundedPosition;

    /// <summary>The velocity we drove with this step, before physics resolved any crash (used for ram damage).</summary>
    public Vector3 LastVelocity { get; private set; }

    /// <summary>Signed speed along the car's nose (negative = reversing).</summary>
    public float ForwardSpeed { get; private set; }
    public float MaxSpeed => maxSpeed;
    public bool IsGrounded { get; private set; }
    public Rigidbody Body => rb;

    /// <summary>Multiplies top speed and acceleration (Turbo sets this).</summary>
    public float SpeedMultiplier { get; set; } = 1f;
    /// <summary>Multiplies grip (knockback lowers it briefly so hits slide the car).</summary>
    public float GripMultiplier { get; set; } = 1f;
    /// <summary>When false the car coasts to a stop (used for countdown, results, eliminated).</summary>
    public bool InputEnabled { get; set; } = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Extra solver passes keep pile-ups (car -> parked car -> wall) from shaking or sinking in.
        rb.solverIterations = 12;
        rb.solverVelocityIterations = 4;
        input = GetComponent<CarInputSource>();
        lastGroundedPosition = transform.position;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Ray starts inside our own collider, so it never hits ourselves.
        IsGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 1.0f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        Vector3 v = rb.linearVelocity;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float f = Vector3.Dot(v, fwd);
        float s = Vector3.Dot(v, right);

        // Safety net: if physics ever pushes us through the world, go back to the last good spot.
        if (IsGrounded && transform.position.y > -0.5f) lastGroundedPosition = transform.position;
        else if (transform.position.y < -2f) { Teleport(lastGroundedPosition + Vector3.up * 0.3f, transform.rotation); return; }

        if (!IsGrounded) { ForwardSpeed = f; LastVelocity = v; return; }

        // While knocked back, ignore the driver and slide (low grip).
        bool stunned = stunTimer > 0f;
        if (stunned) stunTimer -= dt;
        bool canDrive = InputEnabled && input != null && !stunned;
        float throttle = canDrive ? Mathf.Clamp(input.Throttle, -1f, 1f) : 0f;
        float steer = canDrive ? Mathf.Clamp(input.Steer, -1f, 1f) : 0f;
        float gripNow = grip * GripMultiplier * (stunned ? 0.15f : 1f);
        float top = maxSpeed * SpeedMultiplier;

        if (throttle > 0.01f)
        {
            if (f < -0.5f) f = Mathf.MoveTowards(f, 0f, brakeDeceleration * dt);
            else
            {
                float accel = acceleration * SpeedMultiplier * (1f - 0.6f * Mathf.Clamp01(f / top));
                f = Mathf.MoveTowards(f, top * throttle, accel * dt);
            }
        }
        else if (throttle < -0.01f)
        {
            if (f > 0.5f) f = Mathf.MoveTowards(f, 0f, brakeDeceleration * dt);
            else f = Mathf.MoveTowards(f, maxReverseSpeed * throttle, acceleration * 0.8f * dt);
        }
        else
        {
            f = Mathf.MoveTowards(f, 0f, coastDeceleration * dt);
        }

        s = Mathf.Lerp(s, 0f, 1f - Mathf.Exp(-gripNow * dt));
        rb.linearVelocity = fwd * f + right * s + Vector3.up * Mathf.Min(v.y, 0f);
        LastVelocity = rb.linearVelocity;
        ForwardSpeed = f;

        // Steering: needs some speed, a little weaker at top speed, flips when reversing.
        float absF = Mathf.Abs(f);
        // Holding throttle always gives a bit of steering, so you can turn away from a wall you're pushing into.
        float lowSpeed = Mathf.Max(Mathf.Clamp01(absF / fullTurnSpeed), Mathf.Abs(throttle) * 0.4f);
        float highSpeed = Mathf.Lerp(1f, topSpeedTurnFactor, Mathf.Clamp01(absF / maxSpeed));
        float dir = absF > 0.5f ? Mathf.Sign(f) : (throttle < -0.01f ? -1f : 1f);
        float targetYaw = steer * turnRate * Mathf.Deg2Rad * lowSpeed * highSpeed * dir;
        float yaw = Mathf.Lerp(rb.angularVelocity.y, targetYaw, 1f - Mathf.Exp(-yawResponse * dt));
        rb.angularVelocity = new Vector3(0f, yaw, 0f);
    }

    /// <summary>Shove the car (chest bounce, rams, blasts). It slides without driver control for stunTime.</summary>
    public void ApplyKnockback(Vector3 velocityChange, float stunTime)
    {
        velocityChange.y = 0f;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
        stunTimer = Mathf.Max(stunTimer, stunTime);
    }

    /// <summary>Swap who's driving (player keyboard, AI, or a test script).</summary>
    public void SetInput(CarInputSource source) => input = source;

    /// <summary>Instantly place the car (spawning, restarting).</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
