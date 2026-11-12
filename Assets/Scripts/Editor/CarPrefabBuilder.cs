using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns a car model into a drivable prefab: Rigidbody + capsule collider on the root,
/// the model under a "Visual" child (scaled to ~4 units long, sitting on the ground),
/// a roof mount with both blaster models (hidden until earned), and all the gameplay scripts.
/// </summary>
public static class CarPrefabBuilder
{
    public const string PlayerPrefabPath = "Assets/Prefabs/Cars/PlayerCar.prefab";
    const float TargetLength = 4f;
    // Longest side of each roof blaster, in units (the car is 4 long, ~2.5 wide).
    public static float SmallBlasterSize = 1.4f;
    public static float JackpotBlasterSize = 1.7f;

    [MenuItem("Jackpot Derby/Build Car Prefabs")]
    public static void BuildAll()
    {
        Build("PlayerCar", "Assets/Models/car.glb", "Lucky 7", new Color(0.15f, 0.70f, 0.70f), true);
        AssetDatabase.SaveAssets();
    }

    public static GameObject Build(string name, string modelPath, string displayName, Color color, bool player)
    {
        ModelPrefabUtil.EnsureFolder("Assets/Prefabs/Cars");
        var root = new GameObject(name);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        var model = ModelPrefabUtil.PlaceModel(modelPath, visual, TargetLength);
        if (model == null) { Object.DestroyImmediate(root); return null; }
        Bounds b = ModelPrefabUtil.LocalBounds(model, root.transform);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1000f;
        rb.linearDamping = 0f;
        rb.angularDamping = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // A capsule lying along the car: rounded ends let the car swivel off walls and slide
        // round building corners instead of snagging like a box would.
        var capsule = root.AddComponent<CapsuleCollider>();
        float radius = Mathf.Min(b.size.x * 0.46f, 1.2f);
        capsule.direction = 2; // Z axis
        capsule.radius = radius;
        capsule.height = b.size.z * 0.98f;
        capsule.center = new Vector3(0f, radius, 0f);
        capsule.sharedMaterial = CarPhysicsMaterial();

        // Roof mount for blasters (barrels on the models point along -X, so turn them +90°).
        var mount = new GameObject("BlasterMount").transform;
        mount.SetParent(visual, false);
        mount.localPosition = new Vector3(0f, b.max.y - 0.3f, -0.35f);
        var small = BlasterVisual("BlasterSmall", "Assets/Models/blaster_small.glb", mount, SmallBlasterSize);
        var jackpot = BlasterVisual("BlasterJackpot", "Assets/Models/blaster_jackpot.glb", mount, JackpotBlasterSize);

        root.AddComponent<CarMotor>();
        root.AddComponent<CarVisualTilt>();
        root.AddComponent<Odometer>();
        root.AddComponent<CarHealth>();
        var loadout = root.AddComponent<CarLoadout>();
        ModelPrefabUtil.SetField(loadout, "smallBlasterVisual", small);
        ModelPrefabUtil.SetField(loadout, "jackpotBlasterVisual", jackpot);
        ModelPrefabUtil.SetField(loadout, "mount", mount);

        var id = root.AddComponent<CarIdentity>();
        id.displayName = displayName;
        id.color = color;
        id.isPlayer = player;
        if (player) root.AddComponent<PlayerCarInput>();

        string path = $"Assets/Prefabs/Cars/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[CarPrefabBuilder] {path}: size {b.size}");
        return prefab;
    }

    static GameObject BlasterVisual(string name, string modelPath, Transform mount, float size)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(mount, false);
        ModelPrefabUtil.PlaceModel(modelPath, holder.transform, size, 90f);
        // PlaceModel centres relative to the holder's world position; holder sits at the mount.
        holder.SetActive(false);
        return holder;
    }

    static PhysicsMaterial CarPhysicsMaterial()
    {
        ModelPrefabUtil.EnsureFolder("Assets/Settings/Physics");
        const string path = "Assets/Settings/Physics/CarBody.physicMaterial";
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m == null) { m = new PhysicsMaterial("CarBody"); AssetDatabase.CreateAsset(m, path); }
        m.dynamicFriction = 0f;
        m.staticFriction = 0f;
        m.frictionCombine = PhysicsMaterialCombine.Minimum;
        m.bounciness = 0.2f;
        m.bounceCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(m);
        return m;
    }
}
