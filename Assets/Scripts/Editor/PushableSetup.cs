using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Turns the parked cars on the arena streets into pushable vehicles (PushableVehicle): a Rigidbody
/// that only rolls along the car's length, a frictionless collider, and a carving NavMeshObstacle so
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
        rb.angularDamping = 5f;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
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
