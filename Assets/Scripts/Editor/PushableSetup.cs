using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Turns the parked cars on the arena streets into pushable vehicles (PushableVehicle): a Rigidbody
/// that rolls along the car's length (and slides/swings a little when hit side-on), a frictionless collider, and a carving NavMeshObstacle so
/// the AI steers round wherever the car has rolled to (the cars are left out of the baked NavMesh).
/// ArenaBuilder calls Make() as it parks them; the menu item converts an already built arena.
/// The scenery city outside the arena stays static.
/// </summary>
public static class PushableSetup
{
    const string SlickPath = "Assets/Materials/Physics/ParkedCarSlick.physicMaterial";

    /// <summary>Mass per model (the player's car is 1000).</summary>
    static float MassFor(string model) =>
        model.Contains("van") ? 1400f : model.Contains("bus") ? 4000f : model.Contains("wreck") ? 2200f : 900f;

    public static bool IsVehicle(string name) =>
        name.StartsWith("parked_") || name.StartsWith("taxi") || name.StartsWith("city_bus") || name.StartsWith("wrecked_car");

    [MenuItem("Jackpot Derby/Make Parked Cars Pushable")]
    public static void ConvertArena()
    {
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        var props = grid != null ? grid.transform.Find("Props") : null;
        if (props == null) { Debug.LogError("[PushableSetup] No Arena/Props group in the scene."); return; }
        int n = 0;
        foreach (Transform t in props)
            if (IsVehicle(t.name)) { Make(t.gameObject); n++; }
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        Debug.Log($"[PushableSetup] {n} parked vehicles are pushable. Re-bake the AI NavMesh so it leaves them out.");
    }

    // ---------- keeping parked cars clear of everything else ----------

    /// <summary>Gap kept round a parked car (units), so nothing touches it.</summary>
    const float Clearance = 0.2f;
    /// <summary>How far a car may slide along its street to find a clear spot before it is removed.</summary>
    const float MaxSlide = 6f;

    [MenuItem("Jackpot Derby/Fix Parked Car Overlaps")]
    public static void FixOverlapsMenu()
    {
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) { Debug.LogError("[PushableSetup] No ArenaGrid in the scene."); return; }
        ResolveOverlaps(grid);
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
    }

    /// <summary>
    /// Every parked car (arena streets and the outer lots) that overlaps a bench, lamp, hydrant, other
    /// car or anything else slides along its length to the nearest clear spot; if there is none within
    /// MaxSlide it is removed. ArenaBuilder runs this after building. Returns a one-line report.
    /// </summary>
    public static string ResolveOverlaps(ArenaGrid grid)
    {
        Physics.SyncTransforms();
        var loose = LooseRenderers(grid.transform);
        var cars = new System.Collections.Generic.List<Transform>();
        foreach (var t in grid.GetComponentsInChildren<Transform>(true))
            if (t.parent != null && (t.parent.name == "Props" || t.parent.name == "OuterLots") && IsVehicle(t.name)) cars.Add(t);

        int moved = 0, removed = 0;
        var log = new System.Text.StringBuilder();
        foreach (var car in cars)
        {
            bool street = car.parent.name == "Props";
            if (!Overlaps(car, loose, out string what) && (!street || OnPlainRoad(car, grid))) continue;
            if (what == null) what = "a junction";
            Vector3 origin = car.position;
            Vector3 along = car.forward; along.y = 0f; along.Normalize();
            Undo.RecordObject(car, "Fix parked car overlap");
            bool clear = false;
            for (float step = 0.5f; step <= MaxSlide && !clear; step += 0.5f)
                foreach (float sign in new[] { 1f, -1f })
                {
                    car.position = origin + along * step * sign;
                    Physics.SyncTransforms();
                    if (!Overlaps(car, loose, out _) && (!street || OnPlainRoad(car, grid))) { clear = true; break; }
                }
            if (clear)
            {
                moved++;
                log.Append($"\n  moved {car.name} {Vector3.Distance(origin, car.position):0.0} units off {what}");
            }
            else
            {
                car.position = origin;
                removed++;
                log.Append($"\n  removed {car.name} at {origin:F0} (no room next to {what})");
                Undo.DestroyObjectImmediate(car.gameObject);
                Physics.SyncTransforms();
            }
        }
        string report = $"[PushableSetup] Parked car overlaps: {moved} moved, {removed} removed (of {cars.Count} cars).{log}";
        Debug.Log(report);
        return report;
    }

    /// <summary>
    /// A street car must sit wholly on plain road (not poking into a junction, where cars turn, or
    /// onto a block). Checked at the four corners of its collider.
    /// </summary>
    static bool OnPlainRoad(Transform car, ArenaGrid grid)
    {
        var bc = car.GetComponent<BoxCollider>();
        if (bc == null) return true;
        Vector3 he = bc.size * 0.5f;
        float t = grid.tileSize;
        int w = grid.Width, h = grid.Height;
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
        {
            Vector3 p = car.TransformPoint(bc.center + new Vector3(he.x * sx, 0f, he.z * sz));
            int x = Mathf.RoundToInt(p.x / t + (w - 1) * 0.5f), y = Mathf.RoundToInt((h - 1) * 0.5f - p.z / t);
            if (x < 0 || y < 0 || x >= w || y >= h || grid.Get(x, y) != 'R') return false;
            int arms = 0;
            if (y > 0 && grid.Get(x, y - 1) == 'R') arms++;
            if (y < h - 1 && grid.Get(x, y + 1) == 'R') arms++;
            if (x > 0 && grid.Get(x - 1, y) == 'R') arms++;
            if (x < w - 1 && grid.Get(x + 1, y) == 'R') arms++;
            if (arms >= 3) return false;
        }
        return true;
    }

    /// <summary>Renderers with no collider that a car could still visibly sit inside (trash bags, bushes, trees).</summary>
    static System.Collections.Generic.List<Renderer> LooseRenderers(Transform root)
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.GetComponentInParent<Collider>() != null) continue;
            var b = r.bounds;
            if (b.size.y < 0.3f || b.size.x > 30f || b.size.z > 30f) continue;   // roads, kerbs, lawns, ground
            list.Add(r);
        }
        return list;
    }

    static bool Overlaps(Transform car, System.Collections.Generic.List<Renderer> loose, out string what)
    {
        what = null;
        var bc = car.GetComponent<BoxCollider>();
        if (bc == null) return false;
        Vector3 centre = car.TransformPoint(bc.center);
        Vector3 half = Vector3.Scale(bc.size, car.lossyScale) * 0.5f;
        Vector3 padded = new Vector3(half.x + Clearance, half.y - 0.05f, half.z + Clearance);
        foreach (var h in Physics.OverlapBox(centre, padded, car.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform == car || h.transform.IsChildOf(car)) continue;
            var hb = h.bounds.size;
            if (hb.x > 40f || hb.z > 40f) continue;   // ground slab, invisible edge walls
            what = h.name;
            return true;
        }
        foreach (var r in loose)
        {
            if (r == null || r.transform.IsChildOf(car)) continue;
            Vector3 local = car.InverseTransformPoint(r.bounds.center) - bc.center;
            Vector3 ext = car.InverseTransformVector(r.bounds.extents);
            ext = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z)) * 0.8f;
            Vector3 hb = bc.size * 0.5f;
            if (Mathf.Abs(local.x) < hb.x + Clearance + ext.x && Mathf.Abs(local.z) < hb.z + Clearance + ext.z && Mathf.Abs(local.y) < hb.y + ext.y)
            {
                what = r.name;
                return true;
            }
        }
        return false;
    }

    public static void Make(GameObject go)
    {
        if (go == null) return;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        var box = go.GetComponent<BoxCollider>();
        if (box == null) return;
        box.sharedMaterial = SlickMaterial();

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = MassFor(go.name);
        rb.useGravity = false;                 // it sits on flat ground; Y is frozen
        rb.linearDamping = 0f;                 // PushableVehicle does the rolling resistance
        rb.angularDamping = 1f;                // PushableVehicle scrubs the spin itself
        // Can turn about Y (an off-centre hit swings it round) but never tips or leaves the ground.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        if (go.GetComponent<PushableVehicle>() == null) go.AddComponent<PushableVehicle>();

        var mod = go.GetComponent<NavMeshModifier>();
        if (mod == null) mod = go.AddComponent<NavMeshModifier>();
        mod.ignoreFromBuild = true;

        var obs = go.GetComponent<NavMeshObstacle>();
        if (obs == null) obs = go.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box;
        obs.center = box.center;
        obs.size = box.size;
        obs.carving = true;
        obs.carveOnlyStationary = true;        // re-carve only once it has stopped rolling
        obs.carvingMoveThreshold = 0.3f;
        obs.carvingTimeToStationary = 0.4f;
    }

    static PhysicsMaterial SlickMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SlickPath);
        if (m != null) return m;
        ModelPrefabUtil.EnsureFolder("Assets/Materials/Physics");
        m = new PhysicsMaterial("ParkedCarSlick")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0.1f,
            bounceCombine = PhysicsMaterialCombine.Average,
        };
        AssetDatabase.CreateAsset(m, SlickPath);
        return m;
    }
}
