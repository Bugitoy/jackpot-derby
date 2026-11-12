using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First chest at 10 s, then one every 25 s (max 2 at once, 1 in 4 is a Jackpot chest).
/// Chests appear on open ground (plazas, parks, crossroads) as far from cars as it can manage.
/// </summary>
public class ChestSpawner : MonoBehaviour
{
    [SerializeField] Chest chestPrefab;
    [SerializeField] Chest jackpotChestPrefab;
    [SerializeField] ArenaGrid grid;

    [SerializeField] float firstSpawnTime = 10f;
    [SerializeField] float spawnInterval = 25f;
    [SerializeField] int maxActive = 2;
    [SerializeField, Range(0f, 1f)] float jackpotChance = 0.25f;
    [Tooltip("Try to keep new chests at least this far from every car and chest.")]
    [SerializeField] float preferredClearance = 30f;

    /// <summary>When false, the spawn clock is paused (countdown, results).</summary>
    public bool Running { get; set; } = true;
    /// <summary>Optional filter (the storm will use it so chests don't spawn outside the safe zone).</summary>
    public Func<Vector3, bool> IsAllowed;

    readonly List<Vector3> candidates = new List<Vector3>();
    float clock;
    float nextSpawn;

    void Start()
    {
        if (grid == null) grid = FindAnyObjectByType<ArenaGrid>();
        BuildCandidates();
        nextSpawn = firstSpawnTime;
    }

    void Update()
    {
        if (!Running) return;
        clock += Time.deltaTime;
        if (clock >= nextSpawn)
        {
            nextSpawn = clock + spawnInterval;
            SpawnOne();
        }
    }

    void BuildCandidates()
    {
        candidates.Clear();
        if (grid == null) return;
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            char c = grid.Get(x, y);
            bool crossroads = c == 'R' && CountRoadNeighbours(x, y) >= 4;
            if (c != 'P' && c != 'K' && !crossroads) continue;

            // Parks have trees, plazas have crates: try the centre and a few offsets for a clear spot.
            Vector3 centre = grid.TileCenter(x, y);
            float q = grid.tileSize * 0.25f;
            foreach (var off in new[] { Vector3.zero, new Vector3(q, 0, q), new Vector3(-q, 0, q), new Vector3(q, 0, -q), new Vector3(-q, 0, -q) })
            {
                Vector3 p = centre + off;
                if (!Physics.CheckBox(p + Vector3.up * 1.6f, new Vector3(2.2f, 1.4f, 2.2f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    candidates.Add(p);
                    break;
                }
            }
        }
    }

    int CountRoadNeighbours(int x, int y)
    {
        int n = 0;
        if (grid.Get(x + 1, y) == 'R') n++;
        if (grid.Get(x - 1, y) == 'R') n++;
        if (grid.Get(x, y + 1) == 'R') n++;
        if (grid.Get(x, y - 1) == 'R') n++;
        return n;
    }

    /// <summary>Spawn a chest now (also used by the debug cheat).</summary>
    public Chest SpawnOne()
    {
        if (Chest.Active.Count >= maxActive || candidates.Count == 0) return null;

        var good = new List<Vector3>();
        Vector3 best = candidates[0];
        float bestScore = -1f;
        foreach (var p in candidates)
        {
            if (IsAllowed != null && !IsAllowed(p)) continue;
            float score = Clearance(p);
            if (score >= preferredClearance) good.Add(p);
            if (score > bestScore) { bestScore = score; best = p; }
        }
        Vector3 pos = good.Count > 0 ? good[UnityEngine.Random.Range(0, good.Count)] : best;

        bool jackpot = jackpotChestPrefab != null && UnityEngine.Random.value < jackpotChance;
        var prefab = jackpot ? jackpotChestPrefab : chestPrefab;
        var rot = Quaternion.Euler(0f, 90f * UnityEngine.Random.Range(0, 4), 0f);
        return Instantiate(prefab, pos, rot);
    }

    float Clearance(Vector3 p)
    {
        float min = float.MaxValue;
        foreach (var car in CarIdentity.All) min = Mathf.Min(min, Vector3.Distance(car.transform.position, p));
        foreach (var c in Chest.Active) min = Mathf.Min(min, Vector3.Distance(c.transform.position, p));
        return min;
    }
}
