using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The AI's hands on the wheel. Turns "go there / stop / chase that / back up" into throttle and
/// steering for the same CarMotor the player uses. Uses the NavMesh only to plan the route.
/// Gets itself unstuck by backing up when it's pushing against something.
/// </summary>
[RequireComponent(typeof(CarMotor))]
public class AICarDriver : CarInputSource
{
    public enum Mode { Idle, FollowPath, Chase, Stop, Reverse }

    [SerializeField] float repathInterval = 0.4f;
    [SerializeField] float waypointReach = 4.5f;
    [Tooltip("Steering reaches full lock at this angle to the target.")]
    [SerializeField] float fullSteerAngle = 32f;
    [SerializeField] float stuckSpeed = 1.2f;
    [SerializeField] float stuckTime = 0.8f;
    [SerializeField] float unstickTime = 0.9f;

    public Mode CurrentMode { get; private set; } = Mode.Idle;
    public Vector3 Destination { get; private set; }
    /// <summary>Signed distance driven (forward +, reverse -). The odometer counts the same way.</summary>
    public float Travelled { get; private set; }
    public bool IsStopped => Mathf.Abs(motor.ForwardSpeed) < 0.5f;
    public float Speed => motor.ForwardSpeed;
    public bool IsUnsticking => unstickTimer > 0f;
    /// <summary>0..1: swerve left and right while driving (dodging bolts).</summary>
    public float Weave { get; set; }

    public override float Throttle => throttle;
    public override float Steer => steer;

    CarMotor motor;
    NavMeshPath path;
    Vector3[] corners = new Vector3[0];
    int cornerIndex;
    float repathTimer;
    Vector3 pathDestination = new Vector3(9999f, 0f, 9999f);
    float speedLimit = 1f;
    float throttle, steer;
    float stuckTimer, unstickTimer, unstickThrottle, unstickSteer;
    bool pathValid;

    void Awake()
    {
        motor = GetComponent<CarMotor>();
        path = new NavMeshPath();
    }

    // ---------------- commands ----------------

    /// <summary>Drive to a point along the road network.</summary>
    public void GoTo(Vector3 destination, float speed = 1f)
    {
        speedLimit = Mathf.Clamp01(speed);
        Destination = destination;
        if (CurrentMode != Mode.FollowPath || (destination - pathDestination).sqrMagnitude > 9f) repathTimer = 0f;
        CurrentMode = Mode.FollowPath;
    }

    /// <summary>Drive straight at a point (rams, final approach to a chest).</summary>
    public void Chase(Vector3 point, float speed = 1f)
    {
        speedLimit = Mathf.Clamp01(speed);
        Destination = point;
        CurrentMode = Mode.Chase;
    }

    /// <summary>Brake to a standstill (that's how the odometer gets locked).</summary>
    public void StopCar() => CurrentMode = Mode.Stop;

    /// <summary>Back straight up (used to count the number down).</summary>
    public void ReverseStraight(float speed = 1f)
    {
        speedLimit = Mathf.Clamp01(speed);
        CurrentMode = Mode.Reverse;
    }

    public float DistanceTo(Vector3 p) { p -= transform.position; p.y = 0f; return p.magnitude; }

    // ---------------- driving ----------------

    void FixedUpdate() => Travelled += motor.ForwardSpeed * Time.fixedDeltaTime;

    void Update()
    {
        float dt = Time.deltaTime;
        float f = motor.ForwardSpeed;

        if (unstickTimer > 0f)
        {
            unstickTimer -= dt;
            throttle = unstickThrottle;
            steer = unstickSteer;
            if (unstickTimer <= 0f) repathTimer = 0f;
            return;
        }

        switch (CurrentMode)
        {
            case Mode.Idle:
                throttle = 0f; steer = 0f;
                break;
            case Mode.Stop:
                steer = 0f;
                throttle = f > 0.4f ? -1f : f < -0.4f ? 1f : 0f;
                break;
            case Mode.Reverse:
                steer = 0f;
                throttle = -speedLimit;
                break;
            case Mode.Chase:
                SteerTowards(Destination, false);
                break;
            case Mode.FollowPath:
                repathTimer -= dt;
                if (repathTimer <= 0f) { repathTimer = repathInterval; Repath(); }
                SteerTowards(NextPathPoint(), DistanceTo(Destination) < 6f);
                break;
        }

        // Stuck: pushing but not moving -> back up (or go forward if we were reversing) with opposite lock.
        bool pushing = Mathf.Abs(throttle) > 0.3f && CurrentMode != Mode.Stop && CurrentMode != Mode.Idle;
        if (pushing && Mathf.Abs(f) < stuckSpeed && motor.InputEnabled) stuckTimer += dt;
        else stuckTimer = Mathf.Max(0f, stuckTimer - dt * 2f);
        if (stuckTimer > stuckTime)
        {
            stuckTimer = 0f;
            unstickTimer = unstickTime;
            unstickThrottle = throttle > 0f ? -1f : 1f;
            unstickSteer = Mathf.Abs(steer) > 0.2f ? -Mathf.Sign(steer) : (Random.value < 0.5f ? -1f : 1f);
        }
    }

    void SteerTowards(Vector3 point, bool arriving)
    {
        Vector3 to = point - transform.position; to.y = 0f;
        Vector3 fwd = transform.forward; fwd.y = 0f;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
        float abs = Mathf.Abs(angle);
        float dist = to.magnitude;

        // Target close behind us: back up while turning (three-point turn).
        if (abs > 130f && dist < 9f)
        {
            throttle = -0.8f;
            steer = -Mathf.Sign(angle);
            return;
        }
        steer = Mathf.Clamp(angle / fullSteerAngle + Weave * Mathf.Sin(Time.time * 4.5f + GetHashCode() % 7), -1f, 1f);
        float t = abs < 45f ? 1f : abs < 90f ? 0.7f : 0.5f;
        if (arriving) t = Mathf.Min(t, 0.35f + dist / 10f);
        throttle = t * speedLimit;
    }

    void Repath()
    {
        pathValid = false;
        if (!NavMesh.SamplePosition(transform.position, out var from, 8f, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(Destination, out var to, 12f, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid) return;
        corners = path.corners;
        cornerIndex = corners.Length > 1 ? 1 : 0;
        pathDestination = Destination;
        pathValid = true;
    }

    Vector3 NextPathPoint()
    {
        if (!pathValid || corners.Length == 0) return Destination;
        while (cornerIndex < corners.Length - 1 && DistanceTo(corners[cornerIndex]) < waypointReach) cornerIndex++;
        return corners[cornerIndex];
    }

    void OnDrawGizmosSelected()
    {
        if (corners == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < corners.Length; i++) Gizmos.DrawLine(corners[i - 1] + Vector3.up, corners[i] + Vector3.up);
    }
}
