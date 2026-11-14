using System.Collections.Generic;
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

    public static readonly (string prefab, string model, string name, Color color, string spawn)[] Enemies =
    {
        ("EnemyLucky8", "Assets/Models/enemy_car_red.glb", "Lucky 8", new Color(0.95f, 0.25f, 0.25f), "Spawn_Lucky8"),
        ("EnemyBlackCat13", "Assets/Models/enemy_car_purple.glb", "Black Cat 13", new Color(0.70f, 0.45f, 1f), "Spawn_BlackCat13"),
        ("EnemyDoubleDice11", "Assets/Models/enemy_car_orange.glb", "Double Dice 11", new Color(1f, 0.6f, 0.15f), "Spawn_DoubleDice11"),
    };

    [MenuItem("Jackpot Derby/Build Car Prefabs")]
    public static void BuildAll()
    {
        Build("PlayerCar", "Assets/Models/car.glb", "Lucky 7", new Color(0.25f, 0.90f, 0.85f), true);
        foreach (var e in Enemies) Build(e.prefab, e.model, e.name, e.color, false);
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
        var cargo = FindRoofCargo(model, b, out Vector3 roofCentre);
        foreach (var part in cargo) part.SetActive(false);   // measure the bare roof...
        Bounds body = ModelPrefabUtil.LocalBounds(model, root.transform);
        foreach (var part in cargo) part.SetActive(true);    // ...but the cases stay until a blaster is fitted

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
        mount.localPosition = new Vector3(0f, body.max.y - 0.08f, roofCentre.z);
        var small = BlasterVisual("BlasterSmall", "Assets/Models/blaster_small.glb", mount, SmallBlasterSize, out var smallTurret);
        var jackpot = BlasterVisual("BlasterJackpot", "Assets/Models/blaster_jackpot.glb", mount, JackpotBlasterSize, out var jackpotTurret);

        root.AddComponent<CarMotor>();
        root.AddComponent<CarVisualTilt>();
        var wheels = root.AddComponent<WheelSpinner>();
        ModelPrefabUtil.SetField(wheels, "model", model.transform);
        var wheelModel = SceneryKit.Get("wheel") ?? SceneryKit.BuildOne("wheel");
        ModelPrefabUtil.SetField(wheels, "wheelPrefab", wheelModel);
        root.AddComponent<Odometer>();
        root.AddComponent<CarHealth>();
        var loadout = root.AddComponent<CarLoadout>();
        ModelPrefabUtil.SetField(loadout, "smallBlasterVisual", small);
        ModelPrefabUtil.SetField(loadout, "jackpotBlasterVisual", jackpot);
        ModelPrefabUtil.SetField(loadout, "mount", mount);
        ModelPrefabUtil.SetField(loadout, "smallTurret", smallTurret);
        ModelPrefabUtil.SetField(loadout, "jackpotTurret", jackpotTurret);
        ModelPrefabUtil.SetArray(loadout, "roofCargo", cargo.ToArray());

        var id = root.AddComponent<CarIdentity>();
        id.displayName = displayName;
        id.color = color;
        id.isPlayer = player;

        // Combat.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "RangeRing";
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var ringRenderer = ring.GetComponent<Renderer>();
        ringRenderer.sharedMaterial = CombatAssetBuilder.RangeRingMaterial();
        ringRenderer.enabled = false;
        ModelPrefabUtil.NoShadows(ring);

        var blaster = root.AddComponent<AutoBlaster>();
        ModelPrefabUtil.SetField(blaster, "smallBolt", AssetDatabase.LoadAssetAtPath<Projectile>(CombatAssetBuilder.SmallBoltPath));
        ModelPrefabUtil.SetField(blaster, "jackpotBolt", AssetDatabase.LoadAssetAtPath<Projectile>(CombatAssetBuilder.JackpotBoltPath));
        ModelPrefabUtil.SetField(blaster, "rangeRing", ringRenderer);
        root.AddComponent<RamDamage>();
        root.AddComponent<TurboAbility>();
        root.AddComponent<CarFx>();
        root.AddComponent<CarWreck>();

        if (player) root.AddComponent<PlayerCarInput>();
        else
        {
            root.AddComponent<AICarDriver>();
            var brain = root.AddComponent<AIBrain>();
            ModelPrefabUtil.SetField(brain, "personality", AISetup.Personality(name));
        }

        int carLayer = LayerMask.NameToLayer("Car");
        if (carLayer >= 0) foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = carLayer;

        string path = $"Assets/Prefabs/Cars/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[CarPrefabBuilder] {path}: size {b.size}");
        return prefab;
    }

    /// <summary>
    /// The Tripo car models come in parts; the roof rack and luggage are separate parts that start
    /// high up (above ~2/3 of the car's height). CarLoadout hides them while a blaster is mounted.
    /// roofCentre = centre of the cargo (where the blaster goes).
    /// </summary>
    static System.Collections.Generic.List<GameObject> FindRoofCargo(GameObject model, Bounds total, out Vector3 roofCentre)
    {
        var parts = new System.Collections.Generic.List<GameObject>();
        float cutY = total.min.y + total.size.y * 0.66f;
        Renderer biggest = null;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            if (biggest == null || r.bounds.size.sqrMagnitude > biggest.bounds.size.sqrMagnitude) biggest = r;

        Bounds cargo = default;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (r == biggest || r.bounds.min.y < cutY) continue;
            if (parts.Count == 0) cargo = r.bounds; else cargo.Encapsulate(r.bounds);
            parts.Add(r.gameObject);
        }
        roofCentre = parts.Count > 0 ? cargo.center : new Vector3(0f, 0f, -0.3f);
        return parts;
    }

    static GameObject BlasterVisual(string name, string modelPath, Transform mount, float size, out Transform turret)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(mount, false);
        var model = ModelPrefabUtil.PlaceModel(modelPath, holder.transform, size, 90f);
        // PlaceModel centres the whole model, barrel included, which leaves the base plate behind the
        // mount. Shift it so the base plate (the bottom slice of the model) is centred on the roof.
        if (model != null)
        {
            Vector3 baseCentre = BottomSliceCentre(model, 0.15f) - holder.transform.position;
            model.transform.localPosition -= new Vector3(baseCentre.x, 0f, baseCentre.z);
        }

        // Only the cannon swivels: everything except the flat platform it sits on goes under a
        // "Turret" pivot at the base plate's centre (the holder origin), which AutoBlaster turns.
        turret = new GameObject("Turret").transform;
        turret.SetParent(holder.transform, false);
        if (model != null) SplitPlatform(model, holder.transform, turret);
        holder.SetActive(false);
        return holder;
    }

    /// <summary>
    /// Move the cannon parts of a blaster model under the turret pivot, leaving the platform (wide,
    /// flat parts in the bottom 30% of the model) fixed to the roof.
    /// </summary>
    static void SplitPlatform(GameObject model, Transform holder, Transform turret)
    {
        // The GLB comes in as a prefab instance, whose children can't be re-parented: unpack it
        // (meshes and materials still point at the GLB's assets).
        if (PrefabUtility.IsPartOfPrefabInstance(model))
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
        var parts = new List<(Transform t, Bounds b)>();
        Bounds all = new Bounds();
        bool any = false;
        foreach (var r in renderers)
        {
            Bounds b = LocalBounds(r, holder);
            parts.Add((r.transform, b));
            if (!any) { all = b; any = true; } else all.Encapsulate(b);
        }
        if (!any) return;
        float baseTop = all.min.y + all.size.y * 0.3f;
        foreach (var p in parts)
        {
            bool platform = p.b.max.y <= baseTop && p.b.size.x >= all.size.x * 0.35f && p.b.size.z >= all.size.z * 0.35f;
            if (!platform) p.t.SetParent(turret, true);
        }
    }

    /// <summary>A renderer's mesh bounds in another transform's local space.</summary>
    static Bounds LocalBounds(MeshRenderer r, Transform space)
    {
        var mf = r.GetComponent<MeshFilter>();
        Bounds mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds();
        Matrix4x4 m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
        Bounds b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return b;
    }

    /// <summary>World-space centre (x/z extents) of the vertices in the lowest `fraction` of the model's height.</summary>
    static Vector3 BottomSliceCentre(GameObject model, float fraction)
    {
        var filters = model.GetComponentsInChildren<MeshFilter>();
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var mf in filters)
            foreach (var v in mf.sharedMesh.vertices)
            {
                float y = mf.transform.TransformPoint(v).y;
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
        float cut = minY + (maxY - minY) * fraction;
        Vector3 lo = new Vector3(float.MaxValue, 0f, float.MaxValue), hi = new Vector3(float.MinValue, 0f, float.MinValue);
        foreach (var mf in filters)
            foreach (var v in mf.sharedMesh.vertices)
            {
                Vector3 p = mf.transform.TransformPoint(v);
                if (p.y > cut) continue;
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
            }
        return lo.x == float.MaxValue ? model.transform.position : new Vector3((lo.x + hi.x) / 2f, minY, (lo.z + hi.z) / 2f);
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
