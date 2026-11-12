using UnityEngine;

/// <summary>
/// Leans the car body outward in turns and pitches it when accelerating or braking.
/// Only the "Visual" child moves, so physics is unaffected.
/// </summary>
public class CarVisualTilt : MonoBehaviour
{
    [SerializeField] Transform visual;
    [Tooltip("Body roll in degrees per (rad/s of turning × speed/10).")]
    [SerializeField] float rollAmount = 3.5f;
    [SerializeField] float maxRoll = 7f;
    [Tooltip("Body pitch in degrees per unit/s² of acceleration.")]
    [SerializeField] float pitchAmount = 0.12f;
    [SerializeField] float maxPitch = 4f;
    [SerializeField] float response = 10f;

    Rigidbody rb;
    float lastForwardSpeed;
    float accel;
    float roll, pitch;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (visual == null) visual = transform.Find("Visual");
    }

    void FixedUpdate()
    {
        float f = Vector3.Dot(rb.linearVelocity, transform.forward);
        accel = Mathf.Lerp(accel, (f - lastForwardSpeed) / Time.fixedDeltaTime, 0.3f);
        lastForwardSpeed = f;
    }

    void LateUpdate()
    {
        if (visual == null) return;
        float speed01 = Mathf.Abs(lastForwardSpeed) / 10f;
        float targetRoll = Mathf.Clamp(rb.angularVelocity.y * speed01 * rollAmount, -maxRoll, maxRoll);
        float targetPitch = Mathf.Clamp(-accel * pitchAmount, -maxPitch, maxPitch);
        float k = 1f - Mathf.Exp(-response * Time.deltaTime);
        roll = Mathf.Lerp(roll, targetRoll, k);
        pitch = Mathf.Lerp(pitch, targetPitch, k);
        visual.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }
}
