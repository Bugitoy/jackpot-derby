using UnityEngine;

/// <summary>
/// Makes hits feel big on a car: white flash, squash-and-stretch. Also darkens the car into a wreck,
/// with an explosion, a burst of coins and a smoke column, when it's eliminated.
/// </summary>
public class CarFx : MonoBehaviour
{
    [SerializeField] Transform visual;
    [SerializeField] float flashStrength = 1.6f;
    [SerializeField] Color wreckTint = new Color(0.22f, 0.2f, 0.2f);

    static readonly int Emissive = Shader.PropertyToID("emissiveFactor");
    static readonly int BaseColor = Shader.PropertyToID("baseColorFactor");

    Renderer[] renderers;
    MaterialPropertyBlock props;
    CarHealth health;
    float flash, squash, squashTime;
    bool wrecked, dirty = true;

    void Awake()
    {
        if (visual == null) visual = transform.Find("Visual");
        health = GetComponent<CarHealth>();
        props = new MaterialPropertyBlock();
        var model = visual != null ? visual.Find("Model") : null;
        renderers = (model != null ? model : visual).GetComponentsInChildren<Renderer>(true);
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Eliminated += OnEliminated;
        }
    }

    void OnDamaged(DamageInfo d)
    {
        flash = 1f;
        squash = Mathf.Clamp01(0.4f + d.amount / 25f);
        squashTime = 0f;
    }

    void OnEliminated(CarHealth h, CarIdentity killer)
    {
        wrecked = true;
        dirty = true;
        if (FxLibrary.I != null)
        {
            FxLibrary.I.Explosion(transform.position, 1.3f);
            FxLibrary.I.CoinBurst(transform.position, 28);
            FxLibrary.I.CreateSmoke(transform, new Vector3(0f, 2f, 0.6f));
        }
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (flash > 0f) { flash = Mathf.Max(0f, flash - dt * 7f); dirty = true; }
        if (dirty)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(props);
                props.SetColor(Emissive, Color.white * (flash * flashStrength));
                props.SetColor(BaseColor, wrecked ? wreckTint : Color.white);
                r.SetPropertyBlock(props);
            }
            dirty = flash > 0f;
        }

        if (visual != null)
        {
            if (squash > 0f)
            {
                squashTime += dt;
                float s = squash * Mathf.Exp(-squashTime * 9f) * Mathf.Cos(squashTime * 30f);
                visual.localScale = new Vector3(1f + 0.18f * s, 1f - 0.22f * s, 1f + 0.18f * s);
                if (squashTime > 0.6f) { squash = 0f; visual.localScale = Vector3.one; }
            }
        }
    }
}
