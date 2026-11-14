using UnityEngine;

/// <summary>
/// A parked car left in neutral: ram it and it rolls forward or backward along its length, then
/// slows to a stop on rolling resistance. Its tyres grip sideways, so it never slides sideways or
/// spins. Bumps make a thud and a puff of dust.
/// Set up by the PushableSetup editor script (Rigidbody, frictionless collider, NavMeshObstacle).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableVehicle : MonoBehaviour
{
    [Tooltip("How quickly it slows down while rolling (units per second, per second).")]
    [SerializeField] float rollingResistance = 4f;
    [Tooltip("How hard the tyres stop it sliding sideways (higher = grippier).")]
    [SerializeField] float sideGrip = 40f;
    [SerializeField] float maxRollSpeed = 16f;
    [Tooltip("Hits softer than this (relative speed) make no sound or dust.")]
    [SerializeField] float bumpThreshold = 3.5f;

    Rigidbody rb;
    float lastBump = -1f;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        if (rb.IsSleeping()) return;
        float dt = Time.fixedDeltaTime;
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        float along = Vector3.Dot(v, fwd);
        Vector3 side = (v - fwd * along) * Mathf.Exp(-sideGrip * dt);
        along = Mathf.Clamp(Mathf.MoveTowards(along, 0f, rollingResistance * dt), -maxRollSpeed, maxRollSpeed);
        rb.linearVelocity = fwd * along + side;
        if (Mathf.Abs(along) < 0.05f && side.sqrMagnitude < 0.0025f) rb.Sleep();
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
