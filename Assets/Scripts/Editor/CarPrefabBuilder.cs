using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns a car model into a drivable prefab: Rigidbody + one box collider on the root,
/// the model under a "Visual" child (scaled to ~4 units long, sitting on the ground).
/// </summary>
public static class CarPrefabBuilder
{
    public const string PlayerPrefabPath = "Assets/Prefabs/Cars/PlayerCar.prefab";
    const float TargetLength = 4f;

    [MenuItem("Jackpot Derby/Build Car Prefabs")]
    public static void BuildAll()
    {
        Build("PlayerCar", "Assets/Models/car.glb", true);
        AssetDatabase.SaveAssets();
    }

    public static GameObject Build(string name, string modelPath, bool player)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogError("[CarPrefabBuilder] Missing model " + modelPath); return null; }

        EnsureFolder("Assets/Prefabs", "Cars");
        var root = new GameObject(name);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "Model";
        inst.transform.SetParent(visual, false);
        foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

        // Scale so the longest horizontal side is TargetLength, then sit it on the ground, centred.
        Bounds b = RendererBounds(inst);
        float s = TargetLength / Mathf.Max(b.size.x, b.size.z);
        inst.transform.localScale = Vector3.one * s;
        b = RendererBounds(inst);
        inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = RendererBounds(inst);

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

        root.AddComponent<CarMotor>();
        root.AddComponent<CarVisualTilt>();
        if (player) root.AddComponent<PlayerCarInput>();

        string path = $"Assets/Prefabs/Cars/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[CarPrefabBuilder] {path}: size {b.size}");
        return prefab;
    }

    static PhysicsMaterial CarPhysicsMaterial()
    {
        EnsureFolder("Assets/Settings", "Physics");
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

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
}
