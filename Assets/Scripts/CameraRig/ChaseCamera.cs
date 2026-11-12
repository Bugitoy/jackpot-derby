using UnityEngine;

/// <summary>
/// High three-quarter top-down camera (Squad Busters style). It doesn't rotate with the car;
/// it smoothly follows and looks a little ahead in the direction you're driving.
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;

    [SerializeField] float distance = 58f;
    [Tooltip("Looking-down angle. 90 = straight down.")]
    [SerializeField] float pitch = 55f;
    [Tooltip("Compass direction the camera faces. 0 = north is up on screen, 45 = isometric diagonal.")]
    [SerializeField] float yaw = 0f;
    [SerializeField] float fieldOfView = 40f;
    [SerializeField] float smoothTime = 0.25f;
    [Tooltip("Seconds of velocity to look ahead.")]
    [SerializeField] float lookAhead = 0.35f;
    [SerializeField] float maxLookAhead = 8f;

    /// <summary>Extra offset added on top (screen shake writes here).</summary>
    public Vector3 ShakeOffset { get; set; }

    Vector3 focus, focusVelocity, lead;
    Rigidbody targetBody;
    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) { cam.fieldOfView = fieldOfView; cam.farClipPlane = 400f; }
        SetTarget(target);
    }

    public void SetTarget(Transform t)
    {
        target = t;
        targetBody = t != null ? t.GetComponent<Rigidbody>() : null;
        if (t != null) { focus = t.position; focusVelocity = Vector3.zero; lead = Vector3.zero; }
        Apply();
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desiredLead = Vector3.zero;
        if (targetBody != null)
        {
            Vector3 v = targetBody.linearVelocity; v.y = 0f;
            desiredLead = Vector3.ClampMagnitude(v * lookAhead, maxLookAhead);
        }
        lead = Vector3.Lerp(lead, desiredLead, 1f - Mathf.Exp(-3f * Time.deltaTime));
        focus = Vector3.SmoothDamp(focus, target.position + lead, ref focusVelocity, smoothTime);
        Apply();
    }

    void Apply()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance + ShakeOffset, rot);
    }
}
