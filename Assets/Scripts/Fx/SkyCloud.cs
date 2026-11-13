using UnityEngine;

/// <summary>
/// A cartoon cloud in the sky around the arena: it circles slowly round the middle of the map at its
/// own height and distance, and turns (around the vertical only) to face the camera, so the flat
/// cloud model always shows its painted front.
/// </summary>
public class SkyCloud : MonoBehaviour
{
    [Tooltip("Drift speed along its circle, units per second.")]
    public float speed = 1.2f;
    [Tooltip("Flip if the model's painted side faces -Z.")]
    public bool faceBackwards;

    float radius, angle, height;
    Transform cam;

    void Start()
    {
        Vector3 p = transform.position;
        radius = new Vector2(p.x, p.z).magnitude;
        angle = Mathf.Atan2(p.z, p.x);
        height = p.y;
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        if (radius > 1f) angle += speed / radius * Time.deltaTime;
        transform.position = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        if (cam == null) return;
        Vector3 d = cam.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(faceBackwards ? -d : d, Vector3.up);
    }
}
