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
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    [SerializeField] int scramble = 20;
    [SerializeField] float walkSpeed = 4f;
    [SerializeField] float dashSpeed = 14f;
    [SerializeField] float wanderRadius = 40f;
    [SerializeField] Transform visual;

    NavMeshAgent agent;
    float repick, dashTimer, hop;
    Vector3 visualBase;

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
        if (visual != null) visualBase = visual.localPosition;
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

        // Little trotting hop.
        hop += Time.deltaTime * (agent.velocity.magnitude * 1.6f + 1f);
        if (visual != null) visual.localPosition = visualBase + Vector3.up * Mathf.Abs(Mathf.Sin(hop)) * 0.25f;
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
