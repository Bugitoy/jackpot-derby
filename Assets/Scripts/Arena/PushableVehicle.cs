using UnityEngine;

/// <summary>
/// A parked car left in neutral: ram it and it rolls forward or backward along its length, then
/// slows to a stop on rolling resistance. Shoved from the side it slides a short way as the tyres
/// scrub, and an off-centre hit swings it round a little. It never tips over.
/// Its wheels (the WheelPivots the scenery prefab carries) roll with it.
/// Bumps make a thud and a puff of dust.
/// Set up by the PushableSetup editor script (Rigidbody, frictionless collider, NavMeshObstacle).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableVehicle : MonoBehaviour
{
    [Tooltip("How quickly it slows down while rolling (units per second, per second).")]
    [SerializeField] float rollingResistance = 4f;
    [Tooltip("How quickly a sideways shove scrubs to a stop (units per second, per second). Higher = grippier tyres.")]
    [SerializeField] float sideFriction = 14f;
    [Tooltip("How quickly a spin from an off-centre hit dies away (radians per second, per second).")]
    [SerializeField] float spinFriction = 4f;
    [SerializeField] float maxSpin = 2.5f;
    [SerializeField] float maxRollSpeed = 16f;
    [Tooltip("Hits softer than this (relative speed) make no sound or dust.")]
    [SerializeField] float bumpThreshold = 3.5f;

    Rigidbody rb;
    float lastBump = -1f;
    Transform[] wheels = new Transform[0];
    float wheelRadius = 0.5f, wheelAngle;
    Vector3 lastPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastPos = transform.position;
        var holder = transform.Find("Wheels");
        if (holder == null) return;
        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform pivot in holder) if (pivot.name == "WheelPivot") list.Add(pivot);
        wheels = list.ToArray();
        var far = wheels.Length > 0 ? wheels[0].Find("WheelFar") : null;
        var mf = far != null ? far.GetComponent<MeshFilter>() : null;
        if (mf != null && mf.sharedMesh != null) wheelRadius = Mathf.Max(0.1f, mf.sharedMesh.bounds.size.y * 0.5f * transform.lossyScale.y);
    }

    void LateUpdate()
    {
        if (wheels.Length == 0) return;
        // Turn by the distance actually rolled (exact even when frames are slow or uneven).
        Vector3 p = transform.position;
        float along = Vector3.Dot(p - lastPos, transform.forward);
        lastPos = p;
        if (Mathf.Abs(along) < 1e-4f) return;
        // Rolling forward turns the wheels about the car's +X axis (top moving forward).
        wheelAngle = (wheelAngle + along / wheelRadius * Mathf.Rad2Deg) % 360f;
        var r = Quaternion.AngleAxis(wheelAngle, Vector3.right);
        foreach (var w in wheels) w.localRotation = r;
    }

    void FixedUpdate()
    {
        if (rb.IsSleeping()) return;
        float dt = Time.fixedDeltaTime;
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 v = rb.linearVelocity;
        float along = Vector3.Dot(v, fwd);
        float side = Vector3.Dot(v, right);
        // Rolling on its wheels is easy; being shoved sideways scrubs the tyres and stops much sooner.
        along = Mathf.Clamp(Mathf.MoveTowards(along, 0f, rollingResistance * dt), -maxRollSpeed, maxRollSpeed);
        side = Mathf.Clamp(Mathf.MoveTowards(side, 0f, sideFriction * dt), -maxRollSpeed, maxRollSpeed);
        rb.linearVelocity = fwd * along + right * side;
        // An off-centre hit swings it round a little; the tyres soon stop the spin.
        Vector3 w = rb.angularVelocity;
        float spin = Mathf.Clamp(Mathf.MoveTowards(w.y, 0f, spinFriction * dt), -maxSpin, maxSpin);
        rb.angularVelocity = new Vector3(0f, spin, 0f);
        if (Mathf.Abs(along) < 0.05f && Mathf.Abs(side) < 0.05f && Mathf.Abs(spin) < 0.03f) rb.Sleep();
    }

    void OnCollisionEnter(Collision c)
    {
        if (c.rigidbody == null) return;   // buildings, kerbs: no thud
        float speed = c.relativeVelocity.magnitude;
        if (speed < bumpThreshold || Time.time - lastBump < 0.25f) return;
        lastBump = Time.time;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        GameAudio.Bump(p, Mathf.Clamp01((speed - bumpThreshold) / 15f));
        if (FxLibrary.I != null) FxLibrary.I.Dust(new Vector3(p.x, 0.2f, p.z), Mathf.Lerp(0.6f, 1.3f, speed / 20f));
    }
}
