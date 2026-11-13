using UnityEngine;

/// <summary>
/// Low chase camera just above and behind the car. It swings round smoothly to stay behind the car's
/// heading (its nose, not its velocity, so reversing doesn't flip it), widens a little with speed and
/// Turbo, and slides in front of buildings and walls so they never hide the car.
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;

    [Header("Framing")]
    [SerializeField] float height = 4.2f;
    [SerializeField] float distance = 9.5f;
    [Tooltip("The camera looks at this height above the car...")]
    [SerializeField] float lookHeight = 1.4f;
    [Tooltip("...and this far ahead of it.")]
    [SerializeField] float lookAhead = 5f;
    [SerializeField] float fieldOfView = 60f;

    [Header("Feel")]
    [Tooltip("Seconds for the camera to swing round behind the car after it turns.")]
    [SerializeField] float yawSmoothTime = 0.3f;
    [Tooltip("Seconds for the camera to catch up with the car's position.")]
    [SerializeField] float followSmoothTime = 0.06f;
    [Tooltip("Extra field of view at top speed / with Turbo.")]
    [SerializeField] float speedFov = 5f;
    [SerializeField] float turboFov = 6f;
    [Tooltip("Extra distance behind at top speed.")]
    [SerializeField] float speedPullBack = 1.5f;
    [Tooltip("How much of the shake offset to use (the camera is close, so keep it gentle).")]
    [SerializeField] float shakeScale = 0.35f;

    [Header("Collision")]
    [SerializeField] float collisionRadius = 0.5f;
    [Tooltip("Obstacles shorter than this are ignored (props and cars shouldn't push the camera in).")]
    [SerializeField] float minBlockerHeight = 3.5f;
    [Tooltip("How far the camera rises when a building squeezes it right up to the car.")]
    [SerializeField] float squeezeLift = 4.5f;
    [SerializeField] float farClip = 220f;

    /// <summary>Extra offset added on top (screen shake writes here).</summary>
    public Vector3 ShakeOffset { get; set; }

    Vector3 pivot, pivotVelocity;
    float yaw, yawVelocity, armLength, fovNow;
    Rigidbody targetBody;
    CarMotor motor;
    TurboAbility turbo;
    Camera cam;
    readonly RaycastHit[] hits = new RaycastHit[16];

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) { cam.fieldOfView = fieldOfView; cam.farClipPlane = farClip; cam.nearClipPlane = 0.3f; }
        SetTarget(target);
    }

    public void SetTarget(Transform t)
    {
        target = t;
        targetBody = t != null ? t.GetComponent<Rigidbody>() : null;
        motor = t != null ? t.GetComponent<CarMotor>() : null;
        turbo = t != null ? t.GetComponent<TurboAbility>() : null;
        if (t == null) return;
        // Snap straight behind the car (e.g. looking out of the garage at the start).
        pivot = t.position;
        pivotVelocity = Vector3.zero;
        yaw = HeadingYaw();
        yawVelocity = 0f;
        armLength = distance;
        fovNow = fieldOfView;
        Apply(0f);
    }

    float HeadingYaw()
    {
        Vector3 f = target.forward; f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : yaw;
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;
        pivot = Vector3.SmoothDamp(pivot, target.position, ref pivotVelocity, followSmoothTime);
        yaw = Mathf.SmoothDampAngle(yaw, HeadingYaw(), ref yawVelocity, yawSmoothTime);
        Apply(dt);
    }

    void Apply(float dt)
    {
        float speed01 = 0f;
        if (targetBody != null)
        {
            Vector3 v = targetBody.linearVelocity; v.y = 0f;
            float top = motor != null && motor.MaxSpeed > 0f ? motor.MaxSpeed : 25f;
            speed01 = Mathf.Clamp01(v.magnitude / top);
        }
        bool boosting = turbo != null && turbo.IsActive;

        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        float wanted = distance + speedPullBack * speed01;
        Vector3 lookAt = pivot + Vector3.up * lookHeight + heading * Vector3.forward * lookAhead;
        Vector3 armOrigin = pivot + Vector3.up * lookHeight;
        Vector3 armDir = (heading * new Vector3(0f, height - lookHeight, -wanted)).normalized;
        float armMax = new Vector2(height - lookHeight, wanted).magnitude;

        // Spring arm: snap in front of a building at once, ease back out when the view clears.
        float allowed = BlockedDistance(armOrigin, armDir, armMax);
        armLength = allowed < armLength || dt <= 0f ? allowed : Mathf.Lerp(armLength, allowed, 1f - Mathf.Exp(-4f * dt));

        // When a building pushes the camera in, lift it up over the car instead of into it.
        float squeeze = 1f - Mathf.Clamp01(armLength / armMax);
        Vector3 pos = armOrigin + armDir * armLength + Vector3.up * (squeezeLift * squeeze);
        float targetFov = fieldOfView + speedFov * speed01 + (boosting ? turboFov : 0f);
        fovNow = dt <= 0f ? targetFov : Mathf.Lerp(fovNow, targetFov, 1f - Mathf.Exp(-5f * dt));
        if (cam != null) cam.fieldOfView = fovNow;

        transform.SetPositionAndRotation(pos + ShakeOffset * shakeScale, Quaternion.LookRotation(lookAt - pos, Vector3.up));
    }

    /// <summary>How far the camera can sit along the arm before a tall static obstacle gets in the way.</summary>
    float BlockedDistance(Vector3 origin, Vector3 dir, float max)
    {
        int n = Physics.SphereCastNonAlloc(origin, collisionRadius, dir, hits, max, ~0, QueryTriggerInteraction.Ignore);
        float best = max;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider.attachedRigidbody != null) continue;          // cars and pushable things
            // Invisible edge colliders: the camera may look over the edge into the outer city.
            if (h.collider.TryGetComponent<Renderer>(out var rr) && !rr.enabled) continue;
            if (h.distance <= 0f) continue;                               // already overlapping at the car
            var b = h.collider.bounds.size;
            if (b.y < minBlockerHeight) continue;                         // props, parked cars, benches
            if (b.x < 1.5f && b.z < 1.5f) continue;                       // lamps, poles, traffic lights
            best = Mathf.Min(best, Mathf.Max(2.5f, h.distance - 0.2f));
        }
        return best;
    }
}
