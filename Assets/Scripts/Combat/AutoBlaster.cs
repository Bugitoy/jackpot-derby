using UnityEngine;

/// <summary>
/// Squad Busters-style auto-fire. While armed, the roof turret swings toward the nearest enemy
/// inside the attack range (with a clear line of sight) and fires on its own. No aiming needed.
/// The attack range is shown as a soft ring on the ground.
/// </summary>
[RequireComponent(typeof(CarLoadout))]
public class AutoBlaster : MonoBehaviour
{
    [Header("Small blaster")]
    [SerializeField] float smallRange = 12f;
    [SerializeField] float smallInterval = 0.5f;
    [SerializeField] float smallDamage = 5f;
    [SerializeField] Projectile smallBolt;

    [Header("Jackpot cannon")]
    [SerializeField] float jackpotRange = 15f;
    [SerializeField] float jackpotInterval = 0.7f;
    [SerializeField] float jackpotDamage = 5f;
    [SerializeField] int jackpotBurst = 3;
    [SerializeField] float burstSpacing = 0.09f;
    [SerializeField] Projectile jackpotBolt;

    [Header("Turret")]
    [SerializeField] float turretTurnSpeed = 600f;
    [Tooltip("Turret must point within this many degrees of the target to fire.")]
    [SerializeField] float aimTolerance = 35f;

    [Header("Range ring")]
    [SerializeField] Renderer rangeRing;
    [SerializeField] Color playerRingColor = new Color(0.4f, 1f, 0.9f, 0.35f);
    [SerializeField] Color enemyRingColor = new Color(1f, 0.35f, 0.3f, 0.28f);

    /// <summary>Turbo raises this.</summary>
    public float FireRateMultiplier { get; set; } = 1f;
    public CarHealth Target { get; private set; }
    public float Range => loadout.Tier == BlasterTier.Jackpot ? jackpotRange : smallRange;

    static int losMask = -1;

    CarLoadout loadout;
    CarHealth health;
    CarIdentity identity;
    float cooldown, retarget, turretYaw, burstTimer, recoil;
    int burstLeft;
    MaterialPropertyBlock ringProps;

    void Awake()
    {
        loadout = GetComponent<CarLoadout>();
        health = GetComponent<CarHealth>();
        identity = GetComponent<CarIdentity>();
        if (losMask == -1)
        {
            int carLayer = LayerMask.NameToLayer("Car");
            losMask = Physics.DefaultRaycastLayers & ~(carLayer >= 0 ? 1 << carLayer : 0);
        }
    }

    void Update()
    {
        bool armed = loadout.IsArmed && (health == null || health.IsAlive);
        UpdateRing(armed);
        // Hold fire during the countdown and after the match is decided.
        if (MatchManager.Instance != null && MatchManager.Instance.State != MatchState.Playing) armed = false;
        if (!armed) { Target = null; burstLeft = 0; return; }

        float dt = Time.deltaTime;
        retarget -= dt;
        if (retarget <= 0f) { retarget = 0.1f; Target = FindTarget(); }
        if (Target != null && !Target.IsAlive) Target = null;

        // Swing the turret toward the target (or back to the front).
        float desired = 0f;
        if (Target != null)
        {
            Vector3 d = Target.transform.position - transform.position; d.y = 0f;
            desired = Vector3.SignedAngle(Flat(transform.forward), d, Vector3.up);
        }
        turretYaw = Mathf.MoveTowardsAngle(turretYaw, desired, turretTurnSpeed * dt);
        recoil = Mathf.MoveTowards(recoil, 0f, dt * 4f);
        // Only the cannon swivels and kicks back; its platform stays fixed to the roof.
        var turret = loadout.Turret;
        if (turret != null)
        {
            turret.localRotation = Quaternion.Euler(0f, turretYaw, 0f);
            turret.localScale = new Vector3(1f + recoil * 0.25f, 1f - recoil * 0.2f, 1f + recoil * 0.25f);
        }

        cooldown -= dt * FireRateMultiplier;
        if (burstLeft > 0)
        {
            burstTimer -= dt * FireRateMultiplier;
            if (Target == null) burstLeft = 0;
            else if (burstTimer <= 0f) { FireOne(jackpotBolt, jackpotDamage, 1.2f); burstLeft--; burstTimer = burstSpacing; }
        }
        else if (Target != null && cooldown <= 0f && Mathf.Abs(Mathf.DeltaAngle(turretYaw, desired)) < aimTolerance)
        {
            if (loadout.Tier == BlasterTier.Jackpot)
            {
                cooldown = jackpotInterval;
                burstLeft = jackpotBurst;
                burstTimer = 0f;
            }
            else
            {
                cooldown = smallInterval;
                FireOne(smallBolt, smallDamage, 1f);
            }
        }
    }

    CarHealth FindTarget()
    {
        CarHealth best = null;
        float bestSqr = Range * Range;
        foreach (var other in CarHealth.Alive)
        {
            if (other == health || other == null) continue;
            Vector3 d = other.transform.position - transform.position; d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr > bestSqr) continue;
            if (!HasLineOfSight(other)) continue;
            best = other;
            bestSqr = sqr;
        }
        return best;
    }

    bool HasLineOfSight(CarHealth other)
    {
        Vector3 from = transform.position + Vector3.up * 2.2f;
        Vector3 to = other.transform.position + Vector3.up * 1.5f;
        return !Physics.Linecast(from, to, losMask, QueryTriggerInteraction.Ignore);
    }

    Vector3 MuzzlePosition()
    {
        Transform m = loadout.Turret != null ? loadout.Turret : loadout.Mount != null ? loadout.Mount : transform;
        return m.position + m.forward * 1.0f + Vector3.up * 0.45f;
    }

    void FireOne(Projectile prefab, float damage, float flashScale)
    {
        if (prefab == null || Target == null) return;
        Vector3 muzzle = MuzzlePosition();
        var bolt = Instantiate(prefab, muzzle, Quaternion.LookRotation(Target.transform.position - muzzle));
        bolt.Launch(identity, Target, damage);
        recoil = 1f;
        if (FxLibrary.I != null)
            FxLibrary.I.MuzzleFlash(muzzle, loadout.Tier == BlasterTier.Jackpot ? new Color(1f, 0.75f, 0.2f) : new Color(0.4f, 1f, 0.8f), flashScale);
    }

    void UpdateRing(bool armed)
    {
        if (rangeRing == null) return;
        if (rangeRing.enabled != armed) rangeRing.enabled = armed;
        if (!armed) return;
        float d = Range * 2f;
        rangeRing.transform.localScale = new Vector3(d, d, 1f);
        if (ringProps == null) ringProps = new MaterialPropertyBlock();
        bool isPlayer = identity != null && identity.isPlayer;
        Color c = isPlayer ? playerRingColor : enemyRingColor;
        if (loadout.Tier == BlasterTier.Jackpot && isPlayer) c = new Color(1f, 0.85f, 0.3f, c.a);
        c.a *= Target != null ? 1.4f : 1f;
        ringProps.SetColor("_BaseColor", c);
        rangeRing.SetPropertyBlock(ringProps);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
