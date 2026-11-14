using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A black cat wandering the streets (bad luck!). Touch one and your number is scrambled
/// (random ±20) and unlocked. The cat then dashes off somewhere else.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class BlackCat : MonoBehaviour
{
    /// <summary>(car, new number)</summary>
    public static event Action<GameObject, int> AnyScrambled;
    static readonly System.Collections.Generic.Dictionary<Odometer, float> lastScramble = new System.Collections.Generic.Dictionary<Odometer, float>();
    public static readonly System.Collections.Generic.List<BlackCat> All = new System.Collections.Generic.List<BlackCat>();

    // This project enters Play mode without reloading scripts, so static events and lists would keep
    // the previous session's destroyed objects (whose handlers then throw). Start every session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AnyScrambled = null; lastScramble.Clear(); All.Clear(); }
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    [SerializeField] int scramble = 20;
    [SerializeField] float walkSpeed = 4f;
    [SerializeField] float dashSpeed = 14f;
    [SerializeField] float wanderRadius = 40f;
    [SerializeField] Transform visual;

    [Header("Walk animation (hinges made by the prefab builder)")]
    [SerializeField] Transform[] frontLegs;
    [SerializeField] Transform[] backLegs;
    [SerializeField] Transform tail;
    [SerializeField] Transform head;
    [Tooltip("Distance covered per full step cycle.")]
    [SerializeField] float strideLength = 1.1f;
    [SerializeField] float frontSwing = 32f;
    [SerializeField] float backSwing = 22f;

    NavMeshAgent agent;
    float repick, dashTimer, gait, move;
    Vector3 visualBase;
    Quaternion[] frontBase, backBase;
    Quaternion tailBase, headBase, visualRotBase;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        agent.angularSpeed = 540f;
        agent.acceleration = 30f;
        agent.radius = 0.6f;
        agent.height = 1.5f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        agent.autoBraking = false;
        if (visual != null) { visualBase = visual.localPosition; visualRotBase = visual.localRotation; }
        frontBase = Bases(frontLegs);
        backBase = Bases(backLegs);
        if (tail != null) tailBase = tail.localRotation;
        if (head != null) headBase = head.localRotation;
    }

    static Quaternion[] Bases(Transform[] ts)
    {
        if (ts == null) return new Quaternion[0];
        var q = new Quaternion[ts.Length];
        for (int i = 0; i < ts.Length; i++) q[i] = ts[i] != null ? ts[i].localRotation : Quaternion.identity;
        return q;
    }

    void Start()
    {
        if (NavMesh.SamplePosition(transform.position, out var hit, 10f, NavMesh.AllAreas)) agent.Warp(hit.position);
        PickDestination(wanderRadius);
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;
        repick -= Time.deltaTime;
        if (dashTimer > 0f) { dashTimer -= Time.deltaTime; if (dashTimer <= 0f) agent.speed = walkSpeed; }
        if (repick <= 0f || (!agent.pathPending && agent.remainingDistance < 1.5f)) PickDestination(wanderRadius);

        Animate();
    }

    /// <summary>
    /// Procedural walk: legs swing in diagonal pairs (front-left with back-right), the body bobs and
    /// waddles once per step, the head nods and the tail sways. Faster when dashing, still when stopped.
    /// </summary>
    void Animate()
    {
        float dt = Time.deltaTime;
        float speed = agent.velocity.magnitude;
        move = Mathf.MoveTowards(move, Mathf.Clamp(speed / walkSpeed, 0f, 1.4f), dt * 4f);
        gait += dt * (speed / strideLength) * Mathf.PI * 2f;
        float s = Mathf.Sin(gait);
        float t = Time.time;

        for (int i = 0; i < frontLegs.Length; i++)
            if (frontLegs[i] != null) frontLegs[i].localRotation = frontBase[i] * Quaternion.Euler((i % 2 == 0 ? s : -s) * frontSwing * move, 0f, 0f);
        for (int i = 0; i < backLegs.Length; i++)
            if (backLegs[i] != null) backLegs[i].localRotation = backBase[i] * Quaternion.Euler((i % 2 == 0 ? -s : s) * backSwing * move, 0f, 0f);

        if (visual != null)
        {
            float bob = Mathf.Abs(Mathf.Cos(gait)) * 0.09f * move;
            visual.localPosition = visualBase + Vector3.up * bob;
            // Lean forward into a prowl while moving (the model is a sitting cat), plus a step rock and waddle.
            float lean = 14f * Mathf.Min(move, 1f);
            visual.localRotation = visualRotBase * Quaternion.Euler(lean + Mathf.Sin(gait * 2f) * 2.5f * move, 0f, s * 3.5f * move);
        }
        if (head != null)
            head.localRotation = headBase * Quaternion.Euler(Mathf.Sin(gait * 2f + 0.6f) * 6f * move, Mathf.Sin(t * 0.8f) * 14f * (1.2f - move), 0f);
        if (tail != null)
            tail.localRotation = tailBase * Quaternion.Euler(Mathf.Sin(gait + 1.2f) * 6f * move, Mathf.Sin(t * 2.6f) * 16f + s * 8f * move, 0f);
    }

    void PickDestination(float radius)
    {
        repick = UnityEngine.Random.Range(5f, 9f);
        var storm = StormController.Instance;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = transform.position + new Vector3(UnityEngine.Random.Range(-radius, radius), 0f, UnityEngine.Random.Range(-radius, radius));
            if (storm != null && !storm.IsInsideTarget(p, 3f)) p = storm.ClosestSafePoint(p);
            if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) { agent.SetDestination(hit.position); return; }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var car = other.attachedRigidbody.gameObject;
        var health = car.GetComponent<CarHealth>();
        var odo = car.GetComponent<Odometer>();
        if (health == null || !health.IsAlive || odo == null || dashTimer > 0f) return;
        // One scramble per car every few seconds, however many cats it bumps.
        if (lastScramble.TryGetValue(odo, out float t) && Time.time - t < 3f) return;
        lastScramble[odo] = Time.time;

        odo.Scramble(scramble);
        AnyScrambled?.Invoke(car, odo.Number);
        if (FxLibrary.I != null) FxLibrary.I.HitPuff(transform.position + Vector3.up, new Color(0.7f, 0.4f, 1f), 1.2f);

        // Dash away from the car.
        dashTimer = 2.5f;
        agent.speed = dashSpeed;
        Vector3 away = transform.position - car.transform.position; away.y = 0f;
        Vector3 target = transform.position + away.normalized * 25f;
        if (NavMesh.SamplePosition(target, out var hit, 10f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        repick = 3f;
    }
}
