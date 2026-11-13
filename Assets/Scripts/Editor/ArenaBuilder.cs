using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the arena from the ArenaGrid text layout using the real models (light prefabs made by
/// SceneryKit): textured roads that line up, sidewalks, buildings facing their street with rooftop
/// dressing, street props and parked cars for cover, parks, plazas, the central roundabout with a
/// giant slot machine, the four corner garages, and the walled edge with a ring of city outside.
/// Re-running it wipes and rebuilds everything under the Arena object.
/// </summary>
public static class ArenaBuilder
{
    const string MatFolder = "Assets/Materials/Arena";

    static readonly string[] BuildingPool =
    {
        "apartment_brick", "apartment_cream", "apartment_cream_2", "apartment_teal",
        "store_cafe", "store_grocer", "store_laundromat", "store_mechanic", "store_pawn",
        "house_small", "house_small_yellow", "house_tall", "garage_small",
    };
    static readonly string[] RoofPool = { "roof_ac_unit", "roof_vents", "roof_antennas", "roof_water_tower" };
    static readonly string[] SignPool = { "roof_billboard", "neon_sign_clover", "neon_sign_dice" };
    static readonly string[] SidewalkPool =
    {
        "fire_hydrant", "mailbox", "park_bench", "trash_bags", "potted_plants", "vending_machine",
        "transformer_box", "pole_wood", "pole_concrete", "trash_bags", "slot_machine", "bush_large",
    };
    static readonly string[] ShoulderPool = { "parked_sedan", "parked_van", "taxi", "parked_sedan", "taxi" };
    static readonly string[] ClutterPool = { "dumpster", "pallet_stack", "metal_drums", "oil_barrel", "barrier", "cone" };

    struct GarageInfo { public string name, model; public Color color; public int x, y; }

    static Dictionary<string, Material> mats;
    static System.Random rand;

    [MenuItem("Jackpot Derby/Build Arena")]
    public static void BuildArena()
    {
        mats = new Dictionary<string, Material>();
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) grid = new GameObject("Arena").AddComponent<ArenaGrid>();
        grid.Parse(true);
        Transform root = grid.transform;
        for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
        ConfigureGroundTextures();

        rand = new System.Random(grid.seed);
        float t = grid.tileSize;
        int w = grid.Width, h = grid.Height;

        Transform ground = Group(root, "Ground");
        Transform roads = Group(root, "Roads");
        Transform blocks = Group(root, "Blocks");
        Transform props = Group(root, "Props");
        Transform open = Group(root, "PlazasAndParks");
        Transform garages = Group(root, "Garages");
        Transform walls = Group(root, "Walls");
        Transform outside = Group(root, "OutsideCity");
        Transform spawns = Group(root, "SpawnPoints");

        // Ground slab under everything (its collider is what cars drive on).
        var g = Box(ground, "GroundSlab", new Vector3(0, -0.5f, 0), new Vector3(w * t + 120, 1, h * t + 120), TexMat("Ground", "tex_grass", new Color(0.55f, 0.62f, 0.50f), 40f));
        g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var garageDefs = new[]
        {
            new GarageInfo { name = "Lucky 7",        model = "garage_lucky7",       color = new Color(0.15f, 0.70f, 0.70f), x = 0,     y = h - 1 },
            new GarageInfo { name = "Lucky 8",        model = "garage_lucky8",       color = new Color(0.85f, 0.20f, 0.20f), x = 0,     y = 0 },
            new GarageInfo { name = "Black Cat 13",   model = "garage_blackcat13",   color = new Color(0.45f, 0.25f, 0.65f), x = w - 1, y = 0 },
            new GarageInfo { name = "Double Dice 11", model = "garage_doubledice11", color = new Color(0.95f, 0.50f, 0.10f), x = w - 1, y = h - 1 },
        };

        // Landmarks so each district is recognisable (tile -> model).
        var landmarks = new Dictionary<Vector2Int, string>
        {
            { new Vector2Int(10, 4), "casino" },
            { new Vector2Int(6, 6), "arcade_building" },
            { new Vector2Int(6, 4), "charm_shop" },
            { new Vector2Int(10, 6), "store_pawn" },
        };

        var roundabout = new HashSet<Vector2Int>();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (grid.Get(x, y) == 'X')
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    roundabout.Add(new Vector2Int(x + dx, y + dy));

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            char c = grid.Get(x, y);
            Vector3 p = grid.TileCenter(x, y);
            string id = $"{x}_{y}";
            var key = new Vector2Int(x, y);
            if (c == 'X') { BuildRoundabout(roads, p, t); continue; }
            if (roundabout.Contains(key) && c == 'R') { Flat(roads, $"Asphalt_{id}", p + Vector3.up * 0.02f, t, t, 0f, Mat("Asphalt", new Color(0.16f, 0.21f, 0.21f))); continue; }
            switch (c)
            {
                case 'R': BuildRoad(roads, props, grid, x, y, p, t, id); break;
                case 'P': BuildPlaza(open, p, t, id); break;
                case 'K': BuildPark(open, p, t, id); break;
                case '.':
                    landmarks.TryGetValue(key, out string lm);
                    BuildBlock(blocks, props, grid, x, y, p, t, id, lm);
                    break;
                case 'G':
                    foreach (var gd in garageDefs)
                        if (gd.x == x && gd.y == y) BuildGarage(garages, spawns, grid, gd, p, t);
                    break;
            }
        }

        BuildWalls(walls, outside, w * t, h * t);

        foreach (var group in new[] { ground, roads })
            SetStaticRecursive(group.gameObject);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ArenaBuilder] Built {w}x{h} tiles ({w * t}x{h * t} units).");
    }

    // ---------- roads ----------

    static bool Connects(char c) => c == 'R' || c == 'X' || c == 'G' || c == 'P' || c == 'K';

    static void BuildRoad(Transform parent, Transform props, ArenaGrid grid, int x, int y, Vector3 p, float t, string id)
    {
        bool n = Connects(grid.Get(x, y - 1)), s = Connects(grid.Get(x, y + 1));
        bool e = Connects(grid.Get(x + 1, y)), wv = Connects(grid.Get(x - 1, y));
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (wv ? 1 : 0);

        // Texture orientation (image top = north). Yaw turns N -> E -> S -> W.
        string tex; float yaw;
        if (count >= 4) { tex = "tex_road_intersection"; yaw = 0f; }
        else if (count == 3)
        {
            tex = "tex_road_tjunction"; // base: arms W, E, S (no north)
            yaw = !n ? 0f : !e ? 90f : !s ? 180f : 270f;
        }
        else if (count == 2 && !((n && s) || (e && wv)))
        {
            tex = "tex_road_curve"; // base: arms S, E
            yaw = (s && e) ? 0f : (s && wv) ? 90f : (n && wv) ? 180f : 270f;
        }
        else { tex = "tex_road_straight"; yaw = (e || wv) && !(n || s) ? 90f : 0f; }

        Flat(parent, $"Road_{id}", p + Vector3.up * 0.02f, t, t, yaw, TexMat(tex, tex, Color.white, 1f));

        // Straight roads get parked cars and clutter on the wide shoulders (cover for dogfights).
        if (tex != "tex_road_straight" || count < 2) return;
        bool alongZ = yaw == 0f;
        Vector3 along = alongZ ? Vector3.forward : Vector3.right;
        Vector3 across = alongZ ? Vector3.right : Vector3.forward;
        for (int side = -1; side <= 1; side += 2)
        {
            double roll = rand.NextDouble();
            Vector3 basePos = p + across * side * (t * 0.36f) + along * (float)(rand.NextDouble() - 0.5) * t * 0.4f;
            float faceYaw = (alongZ ? 0f : 90f) + (rand.Next(2) == 0 ? 0f : 180f);
            if (roll < 0.28) Place(props, Pick(ShoulderPool), basePos, faceYaw);
            else if (roll < 0.36) Place(props, Pick(ClutterPool), basePos, faceYaw + (float)rand.NextDouble() * 30f);
        }
    }

    static void BuildRoundabout(Transform parent, Vector3 p, float t)
    {
        float size = t * 3f;
        Flat(parent, "Roundabout_Road", p + Vector3.up * 0.03f, size, size, 0f, TexMat("tex_roundabout", "tex_roundabout", Color.white, 1f));
        // Invisible island collider (the texture draws the grass), with a low curb ring for feel.
        float islandR = size * 0.205f;
        var island = Cylinder(parent, "Roundabout_Island", p + Vector3.up * 1f, new Vector3(islandR * 2f, 1f, islandR * 2f), Mat("Grass", new Color(0.36f, 0.62f, 0.30f)));
        island.GetComponent<Renderer>().enabled = false;
        Object.DestroyImmediate(island.GetComponent<Collider>());
        island.AddComponent<MeshCollider>().convex = true;
        // Landmark: a giant slot machine (stand-in for lucky_tower).
        var slot = Place(parent, "slot_machine", p, 180f);
        if (slot != null) slot.transform.localScale = Vector3.one * 8f;
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f + 0.4f;
            Place(parent, i % 2 == 0 ? "bush_large" : "potted_plants", p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * islandR * 0.75f, i * 60f);
        }
    }

    // ---------- open ground ----------

    static void BuildPlaza(Transform parent, Vector3 p, float t, string id)
    {
        Flat(parent, $"Plaza_{id}", p + Vector3.up * 0.02f, t, t, 0f, TexMat("Plaza", "tex_sidewalk", new Color(0.85f, 0.85f, 0.82f), 3f));
        // A little cover at the edges; the middle stays open for chests and dogfights.
        int n = rand.Next(1, 3);
        for (int i = 0; i < n; i++)
        {
            int edge = rand.Next(4);
            Vector3 dir = edge == 0 ? Vector3.forward : edge == 1 ? Vector3.back : edge == 2 ? Vector3.right : Vector3.left;
            Vector3 side = new Vector3(dir.z, 0, dir.x);
            Vector3 pos = p + dir * t * 0.38f + side * (float)(rand.NextDouble() - 0.5) * t * 0.6f;
            Place(parent, Pick(ClutterPool), pos, (float)rand.NextDouble() * 360f);
        }
    }

    static void BuildPark(Transform parent, Vector3 p, float t, string id)
    {
        Flat(parent, $"Park_{id}", p + Vector3.up * 0.02f, t, t, 0f, TexMat("Park", "tex_grass", Color.white, 2f));
        int trees = 1 + rand.Next(0, 2);
        for (int i = 0; i < trees; i++)
        {
            Vector3 tp = p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.6f, 0f, (float)(rand.NextDouble() - 0.5) * t * 0.6f);
            Place(parent, rand.Next(2) == 0 ? "tree_twisted" : "tree_planter", tp, (float)rand.NextDouble() * 360f);
        }
        if (rand.NextDouble() < 0.6) Place(parent, "park_bench", p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.5f, 0, -t * 0.35f), 180f);
        if (rand.NextDouble() < 0.6) Place(parent, "bush_large", p + new Vector3(t * 0.35f, 0, (float)(rand.NextDouble() - 0.5) * t * 0.5f), (float)rand.NextDouble() * 360f);
    }

    // ---------- city blocks ----------

    static void BuildBlock(Transform parent, Transform props, ArenaGrid grid, int x, int y, Vector3 p, float t, string id, string landmark)
    {
        var slab = Box(parent, $"Sidewalk_{id}", p + Vector3.up * 0.06f, new Vector3(t, 0.12f, t), TexMat("Sidewalk", "tex_sidewalk", Color.white, 2f), false);
        slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        slab.isStatic = true;

        // Face the building toward a street. The camera looks north, so prefer a street to the south.
        var dirs = new[] { (dx: 0, dy: 1, yaw: 180f, v: Vector3.back), (dx: 1, dy: 0, yaw: 90f, v: Vector3.right),
                           (dx: -1, dy: 0, yaw: 270f, v: Vector3.left), (dx: 0, dy: -1, yaw: 0f, v: Vector3.forward) };
        float face = 180f;
        foreach (var d in dirs)
            if (ArenaGrid.IsDrivable(grid.Get(x + d.dx, y + d.dy))) { face = d.yaw; break; }

        bool edge = x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1;
        string model = landmark ?? Pick(BuildingPool);
        var b = Place(parent, model, p, face);
        if (b != null)
        {
            // Fill the block like the reference map: footprint ~10-10.6 of the 12-unit tile.
            Vector3 ms = b.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            float s = (10f + (float)rand.NextDouble() * 0.6f) / Mathf.Max(ms.x, ms.z);
            // A bit squat inside the arena so cars on the street behind stay visible; taller at the edge.
            float sy = s * (edge ? 1.0f + (float)rand.NextDouble() * 0.2f : 0.78f + (float)rand.NextDouble() * 0.15f);
            b.transform.localScale = new Vector3(s, sy, s);
            DressRoof(parent, b, face);
        }

        // Street furniture on the sidewalk edges that face drivable ground.
        foreach (var d in dirs)
        {
            if (!ArenaGrid.IsDrivable(grid.Get(x + d.dx, y + d.dy))) continue;
            Vector3 edgeCentre = p + d.v * (t * 0.5f - 0.45f);
            Vector3 side = new Vector3(d.v.z, 0, d.v.x);
            if (rand.NextDouble() < 0.6) Place(props, "street_lamp", edgeCentre + side * t * 0.4f * (rand.Next(2) == 0 ? 1 : -1), d.yaw);
            if (rand.NextDouble() < 0.5) Place(props, Pick(SidewalkPool), edgeCentre + d.v * 0.9f + side * (float)(rand.NextDouble() - 0.5) * t * 0.5f, d.yaw);
        }
    }

    /// <summary>Scatter rooftop units on the flat parts of a roof (found by ray-casting the mesh).</summary>
    static void DressRoof(Transform parent, GameObject building, float faceYaw)
    {
        var mf = building.GetComponent<MeshFilter>();
        if (mf == null) return;
        var mc = building.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        Bounds bb = building.GetComponent<Renderer>().bounds;
        Bounds mb = mf.sharedMesh.bounds; // model space: front faces +Z
        int wanted = 1 + rand.Next(0, 3);
        for (int attempt = 0; attempt < 12 && wanted > 0; attempt++)
        {
            bool sign = wanted == 1 && rand.NextDouble() < 0.45;
            // Signs sit toward the back half so they read from the street; units anywhere in the middle.
            Vector3 local = new Vector3((float)(rand.NextDouble() - 0.5) * 0.6f, 0, sign ? -0.2f : (float)(rand.NextDouble() - 0.5) * 0.6f);
            Vector3 o = building.transform.TransformPoint(mb.center + Vector3.Scale(local, mb.size));
            var ray = new Ray(new Vector3(o.x, bb.max.y + 5f, o.z), Vector3.down);
            if (!mc.Raycast(ray, out RaycastHit hit, bb.size.y + 10f)) continue;
            if (hit.normal.y < 0.96f || hit.point.y < bb.min.y + bb.size.y * 0.55f) continue;
            string item = sign ? Pick(SignPool) : Pick(RoofPool);
            Place(parent, item, hit.point, sign ? faceYaw : (float)rand.NextDouble() * 360f);
            wanted--;
        }
        Object.DestroyImmediate(mc);
    }

    // ---------- garages ----------

    static void BuildGarage(Transform parent, Transform spawns, ArenaGrid grid, GarageInfo gd, Vector3 p, float t)
    {
        var root = new GameObject("Garage_" + gd.name.Replace(" ", "")).transform;
        root.SetParent(parent, false);
        root.position = p;

        Flat(root, "Apron", p + Vector3.up * 0.02f, t, t, 0f, TexMat("Plaza", "tex_sidewalk", new Color(0.85f, 0.85f, 0.82f), 3f));

        Vector3 openDir = Vector3.zero;
        var dirs = new[] { (dx: 0, dy: -1, v: Vector3.forward), (dx: 0, dy: 1, v: Vector3.back), (dx: 1, dy: 0, v: Vector3.right), (dx: -1, dy: 0, v: Vector3.left) };
        foreach (var d in dirs)
            if (ArenaGrid.IsDrivable(grid.Get(gd.x + d.dx, gd.y + d.dy))) openDir += d.v;
        Vector3 face = openDir.sqrMagnitude > 0 ? openDir.normalized : (-p).normalized;

        // The camera looks north, so a tall garage must never sit south of its car (it would hide it).
        // Top garages back onto the north wall facing south; bottom garages back onto the side wall
        // facing sideways. They poke out past the wall a little; the car starts on the apron in front.
        var gPrefab = SceneryKit.Get(gd.model);
        float depth = gPrefab != null ? gPrefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.z : 9f;
        Vector3 doorDir = gd.y == 0 ? Vector3.back : (gd.x == 0 ? Vector3.right : Vector3.left);
        Vector3 garagePos = p - doorDir * (t * 0.5f) - doorDir * (depth * 0.5f - 3f);
        Place(root, gd.model, garagePos, Quaternion.LookRotation(doorDir).eulerAngles.y);

        var sp = new GameObject("Spawn_" + gd.name.Replace(" ", "")).AddComponent<SpawnPoint>();
        sp.transform.SetParent(spawns, false);
        sp.carName = gd.name;
        sp.color = gd.color;
        sp.transform.SetPositionAndRotation(p + doorDir * 1.2f + Vector3.up * 0.05f, Quaternion.LookRotation(face, Vector3.up));
    }

    // ---------- edge ----------

    static void BuildWalls(Transform parent, Transform outside, float sizeX, float sizeZ)
    {
        var m = Mat("ArenaWall", new Color(0.55f, 0.52f, 0.48f));
        float hgt = 3f, thick = 1f;
        // Invisible physics walls (exact), with the wall models drawn along them.
        var colliders = new[]
        {
            Box(parent, "Wall_North", new Vector3(0, hgt / 2, sizeZ / 2 + thick / 2), new Vector3(sizeX + thick * 2, hgt * 4, thick), m),
            Box(parent, "Wall_South", new Vector3(0, hgt / 2, -sizeZ / 2 - thick / 2), new Vector3(sizeX + thick * 2, hgt * 4, thick), m),
            Box(parent, "Wall_East", new Vector3(sizeX / 2 + thick / 2, hgt / 2, 0), new Vector3(thick, hgt * 4, sizeZ), m),
            Box(parent, "Wall_West", new Vector3(-sizeX / 2 - thick / 2, hgt / 2, 0), new Vector3(thick, hgt * 4, sizeZ), m),
        };
        foreach (var c in colliders) c.GetComponent<Renderer>().enabled = false;

        const float seg = 6f;
        var sides = new[]
        {
            (start: new Vector3(-sizeX / 2, 0, sizeZ / 2 + 0.9f),  dir: Vector3.right,   len: sizeX, yaw: 180f),
            (start: new Vector3(-sizeX / 2, 0, -sizeZ / 2 - 0.9f), dir: Vector3.right,   len: sizeX, yaw: 0f),
            (start: new Vector3(sizeX / 2 + 0.9f, 0, -sizeZ / 2),  dir: Vector3.forward, len: sizeZ, yaw: 270f),
            (start: new Vector3(-sizeX / 2 - 0.9f, 0, -sizeZ / 2), dir: Vector3.forward, len: sizeZ, yaw: 90f),
        };
        foreach (var sd in sides)
        {
            int count = Mathf.CeilToInt(sd.len / seg);
            float step = sd.len / count;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = sd.start + sd.dir * (step * (i + 0.5f));
                var wall = Place(parent, i % 5 == 2 ? "retaining_wall" : "arena_wall", pos, sd.yaw, false);
                if (wall != null) wall.transform.localScale = new Vector3(step / seg * 1.02f, 1f, 1f);
            }

            // A ring of city outside the walls so the edge doesn't look like the end of the world.
            Vector3 outward = Quaternion.Euler(0, sd.yaw, 0) * Vector3.back;
            // Corners stay clear: the garages poke out there.
            for (float d = 17f; d < sd.len - 15f; d += 11f)
            {
                Vector3 pos = sd.start + sd.dir * d + outward * 7.5f;
                if (d > sd.len * 0.45f && d < sd.len * 0.55f) { Place(outside, "tunnel_entrance", pos, sd.yaw, false); d += 4f; continue; }
                var b = Place(outside, Pick(BuildingPool), pos, sd.yaw, false);
                if (b != null)
                {
                    float s = 1.2f + (float)rand.NextDouble() * 0.25f;
                    // South of the arena sits between the camera and the play area: keep it low.
                    b.transform.localScale = new Vector3(s, sd.yaw == 0f ? s * 0.45f : s, s);
                }
            }
        }
    }

    // ---------- helpers ----------

    static string Pick(string[] pool) => pool[rand.Next(pool.Length)];

    /// <summary>Instantiate a scenery prefab at a world position, turned so its front faces yaw.</summary>
    static GameObject Place(Transform parent, string name, Vector3 pos, float yaw, bool keepCollider = true)
    {
        var prefab = SceneryKit.Get(name);
        if (prefab == null) { Debug.LogWarning("[ArenaBuilder] Missing scenery prefab " + name + " (run Build Scenery Prefabs)"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (!keepCollider) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return go;
    }

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Box(Transform parent, string name, Vector3 worldCenter, Vector3 size, Material m, bool collider = true)
        => Prim(PrimitiveType.Cube, parent, name, worldCenter, size, m, collider);

    static GameObject Cylinder(Transform parent, string name, Vector3 worldCenter, Vector3 scale, Material m)
        => Prim(PrimitiveType.Cylinder, parent, name, worldCenter, scale, m, true);

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 worldCenter, Vector3 scale, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldCenter;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>Flat upward-facing quad (image top = north before yaw), no collider, no shadows.</summary>
    static GameObject Flat(Transform parent, string name, Vector3 worldCenter, float sizeX, float sizeZ, float yaw, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(worldCenter, Quaternion.Euler(90f, yaw, 0f));
        go.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static Material Mat(string name, Color c) => TexMat(name, null, c, 1f);

    static Material TexMat(string name, string texName, Color c, float tiling)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        ModelPrefabUtil.EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        if (texName != null)
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texName}.jpg"));
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    /// <summary>Ground textures: full 2K, crunch-compressed; clamp the road tiles so seams line up.</summary>
    static void ConfigureGroundTextures()
    {
        foreach (var n in new[] { "tex_road_straight", "tex_road_intersection", "tex_road_tjunction", "tex_road_curve", "tex_roundabout", "tex_sidewalk", "tex_grass" })
        {
            var imp = AssetImporter.GetAtPath($"Assets/Textures/{n}.jpg") as TextureImporter;
            if (imp == null) continue;
            var wrap = n.StartsWith("tex_road") || n == "tex_roundabout" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (imp.maxTextureSize == 2048 && imp.wrapMode == wrap && imp.crunchedCompression) continue;
            imp.maxTextureSize = 2048;
            imp.wrapMode = wrap;
            imp.anisoLevel = 4;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.crunchedCompression = true;
            imp.compressionQuality = 90;
            imp.SaveAndReimport();
        }
    }

    static void SetStaticRecursive(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject);
    }
}
