using UnityEngine;

/// <summary>
/// A chunky glowing blaster bolt. Flies in a slight arc and homes onto its target, so auto-attacks
/// land unless the target escaped out of range before firing. Damage is applied on arrival.
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 36f;
    [SerializeField] float arcHeight = 1.4f;
    [SerializeField] Color hitColor = new Color(0.3f, 1f, 0.7f);
    [SerializeField] float hitScale = 1f;
    [SerializeField] float spin = 720f;

    CarIdentity owner;
    CarHealth target;
    float damage;
    Vector3 start, aimPoint;
    float t, duration;

    public void Launch(CarIdentity owner, CarHealth target, float damage)
    {
        this.owner = owner;
        this.target = target;
        this.damage = damage;
        start = transform.position;
        aimPoint = target.transform.position + Vector3.up * 1.3f;
        duration = Mathf.Max(0.08f, Vector3.Distance(start, aimPoint) / speed);
        t = 0f;
    }

    void Update()
    {
        if (target != null && target.IsAlive) aimPoint = target.transform.position + Vector3.up * 1.3f;
        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);
        Vector3 p = Vector3.Lerp(start, aimPoint, k) + Vector3.up * arcHeight * 4f * k * (1f - k);
        Vector3 step = p - transform.position;
        if (step.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(step) * Quaternion.Euler(0f, 0f, Time.time * spin);
        transform.position = p;

        if (t >= 1f)
        {
            if (target != null && target.IsAlive) target.TakeDamage(damage, owner, DamageKind.Blaster, p);
            if (FxLibrary.I != null) FxLibrary.I.HitPuff(p, hitColor, hitScale);
            Destroy(gameObject);
        }
    }
}
