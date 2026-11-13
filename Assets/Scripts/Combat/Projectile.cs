using UnityEngine;

/// <summary>
/// A chunky glowing blaster bolt. Fired at where the target is heading (leading it), flies in a slight
/// arc with only light homing, so a target that keeps driving straight gets hit but one that swerves
/// can dodge. Damage applies on arrival if the target is still close to the impact point.
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 36f;
    [SerializeField] float arcHeight = 1.4f;
    [SerializeField] Color hitColor = new Color(0.3f, 1f, 0.7f);
    [SerializeField] float hitScale = 1f;
    [SerializeField] float spin = 720f;
    [Tooltip("0 = goes exactly where it was aimed, 1 = follows the target perfectly (never misses).")]
    [SerializeField, Range(0f, 1f)] float homing = 0.2f;
    [Tooltip("The target must be within this distance of where the bolt lands to take damage.")]
    [SerializeField] float hitRadius = 2.1f;

    /// <summary>Debug counters for balancing (shots fired / shots that hit).</summary>
    public static int Fired, Hits;

    CarIdentity owner;
    CarHealth target;
    float damage;
    Vector3 start, leadPoint;
    float t, duration;

    public void Launch(CarIdentity owner, CarHealth target, float damage)
    {
        this.owner = owner;
        this.target = target;
        this.damage = damage;
        start = transform.position;
        Vector3 aim = TargetPoint();
        duration = Mathf.Max(0.08f, Vector3.Distance(start, aim) / speed);
        // Lead the target: aim where it will be if it keeps going the same way.
        var rb = target.GetComponent<Rigidbody>();
        Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero; v.y = 0f;
        leadPoint = aim + v * duration;
        t = 0f;
        Fired++;
    }

    Vector3 TargetPoint() => target.transform.position + Vector3.up * 1.3f;

    void Update()
    {
        Vector3 aimPoint = target != null && target.IsAlive ? Vector3.Lerp(leadPoint, TargetPoint(), homing) : leadPoint;
        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);
        Vector3 p = Vector3.Lerp(start, aimPoint, k) + Vector3.up * arcHeight * 4f * k * (1f - k);
        Vector3 step = p - transform.position;
        if (step.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(step) * Quaternion.Euler(0f, 0f, Time.time * spin);
        transform.position = p;

        if (t >= 1f)
        {
            bool hit = target != null && target.IsAlive && Vector3.Distance(TargetPoint(), p) <= hitRadius;
            if (hit) { target.TakeDamage(damage, owner, DamageKind.Blaster, p); Hits++; }
            if (FxLibrary.I != null)
            {
                if (hit) FxLibrary.I.HitPuff(p, hitColor, hitScale);
                else { FxLibrary.I.Dust(new Vector3(p.x, 0.2f, p.z), 0.6f); FxLibrary.I.MuzzleFlash(p, hitColor, 0.8f); }
            }
            Destroy(gameObject);
        }
    }
}
