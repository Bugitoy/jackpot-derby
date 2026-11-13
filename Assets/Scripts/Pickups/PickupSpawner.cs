using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the streets stocked: black cats from the start, dice (re-roll) and clovers (+30 health).
/// Clovers: one every 40 s, max 2. Dice: one every 20 s, max 2. Never spawns in the storm or on top of cars.
/// </summary>
public class PickupSpawner : MonoBehaviour
{
    [SerializeField] ArenaGrid grid;
    [SerializeField] Pickup dicePrefab;
    [SerializeField] Pickup cloverPrefab;
    [SerializeField] BlackCat catPrefab;

    [SerializeField] int cats = 3;
    [SerializeField] float firstDice = 15f, diceInterval = 20f;
    [SerializeField] int maxDice = 2;
    [SerializeField] float firstClover = 40f, cloverInterval = 40f;
    [SerializeField] int maxClovers = 2;
    [SerializeField] float minDistanceFromCars = 15f;

    readonly List<Vector3> points = new List<Vector3>();
    float clock, nextDice, nextClover;
    bool catsSpawned;

    void Start()
    {
        if (grid == null) grid = FindAnyObjectByType<ArenaGrid>();
        if (grid != null)
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                char c = grid.Get(x, y);
                if (c == 'R' || c == 'P' || c == 'K') points.Add(grid.TileCenter(x, y));
            }
        nextDice = firstDice;
        nextClover = firstClover;
    }

    void Update()
    {
        var mm = MatchManager.Instance;
        if (mm != null && mm.State != MatchState.Playing) return;
        if (!catsSpawned) { catsSpawned = true; for (int i = 0; i < cats; i++) SpawnCat(); }

        clock += Time.deltaTime;
        if (clock >= nextDice) { nextDice = clock + diceInterval; TrySpawn(dicePrefab, PickupKind.Dice, maxDice); }
        if (clock >= nextClover) { nextClover = clock + cloverInterval; TrySpawn(cloverPrefab, PickupKind.Clover, maxClovers); }
    }

    void TrySpawn(Pickup prefab, PickupKind kind, int max)
    {
        if (prefab == null) return;
        int count = 0;
        foreach (var p in Pickup.Active) if (p.Kind == kind) count++;
        if (count >= max) return;
        if (TryPickPoint(out var pos)) Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
    }

    void SpawnCat()
    {
        if (catPrefab == null) return;
        for (int i = 0; i < 10; i++)
        {
            if (!TryPickPoint(out var pos)) return;
            bool spread = true;
            foreach (var other in BlackCat.All)
                if (Vector3.Distance(other.transform.position, pos) < 35f) { spread = false; break; }
            if (!spread) continue;
            Instantiate(catPrefab, pos, Quaternion.identity);
            return;
        }
    }

    bool TryPickPoint(out Vector3 pos)
    {
        var storm = StormController.Instance;
        for (int attempt = 0; attempt < 30 && points.Count > 0; attempt++)
        {
            Vector3 p = points[Random.Range(0, points.Count)] + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            if (storm != null && !storm.IsInsideTarget(p, 4f)) continue;
            bool clear = true;
            foreach (var car in CarIdentity.All)
                if (Vector3.Distance(car.transform.position, p) < minDistanceFromCars) { clear = false; break; }
            if (!clear) continue;
            if (Physics.CheckSphere(p + Vector3.up * 1.2f, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            pos = p;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }
}
