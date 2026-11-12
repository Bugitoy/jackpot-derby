using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the graybox arena from the ArenaGrid text layout: roads, sidewalks, box buildings,
/// plazas, parks, the central roundabout, corner garages, perimeter walls and spawn points.
/// Re-running it wipes and rebuilds everything under the Arena object.
/// </summary>
public static class ArenaBuilder
{
    const string MatFolder = "Assets/Materials/Graybox";

    static readonly Color[] BuildingPalette =
    {
        new Color(0.20f, 0.55f, 0.55f), // teal
        new Color(0.93f, 0.76f, 0.32f), // yellow
        new Color(0.92f, 0.86f, 0.72f), // cream
        new Color(0.76f, 0.32f, 0.26f), // red
        new Color(0.60f, 0.35f, 0.27f), // brick
        new Color(0.55f, 0.66f, 0.52f), // sage
    };

    struct GarageInfo { public string name; public Color color; public int x, y; }

    static Dictionary<string, Material> mats;

    [MenuItem("Jackpot Derby/Build Graybox Arena")]
    public static void BuildArena()
    {
        mats = new Dictionary<string, Material>();
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) grid = new GameObject("Arena").AddComponent<ArenaGrid>();
        grid.Parse(true);
        Transform root = grid.transform;
        for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);

        var rand = new System.Random(grid.seed);
        float t = grid.tileSize;
        int w = grid.Width, h = grid.Height;

        Transform ground = Group(root, "Ground");
        Transform roads = Group(root, "Roads");
        Transform blocks = Group(root, "Blocks");
        Transform open = Group(root, "PlazasAndParks");
        Transform garages = Group(root, "Garages");
        Transform walls = Group(root, "Walls");
        Transform spawns = Group(root, "SpawnPoints");

        // Ground slab under everything (its collider is what cars drive on).
        var g = Box(ground, "GroundSlab", new Vector3(0, -0.5f, 0), new Vector3(w * t + 40, 1, h * t + 40), Mat("Ground", new Color(0.32f, 0.36f, 0.30f)));
        g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var garageDefs = new[]
        {
            new GarageInfo { name = "Lucky 7",       color = new Color(0.15f, 0.70f, 0.70f), x = 0,     y = h - 1 },
            new GarageInfo { name = "Lucky 8",       color = new Color(0.85f, 0.20f, 0.20f), x = 0,     y = 0 },
            new GarageInfo { name = "Black Cat 13",  color = new Color(0.45f, 0.25f, 0.65f), x = w - 1, y = 0 },
            new GarageInfo { name = "Double Dice 11", color = new Color(0.95f, 0.50f, 0.10f), x = w - 1, y = h - 1 },
        };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            char c = grid.Get(x, y);
            Vector3 p = grid.TileCenter(x, y);
            string id = $"{x}_{y}";
            switch (c)
            {
                case 'R': BuildRoad(roads, grid, x, y, p, t, id); break;
                case 'X': BuildRoundabout(roads, p, t); break;
                case 'P': BuildPlaza(open, p, t, id, rand); break;
                case 'K': BuildPark(open, p, t, id, rand); break;
                case '.': BuildBlock(blocks, grid, x, y, p, t, id, rand); break;
                case 'G':
                    foreach (var gd in garageDefs)
                        if (gd.x == x && gd.y == y) BuildGarage(garages, spawns, grid, gd, p, t);
                    break;
            }
        }

        BuildWalls(walls, w * t, h * t);

        foreach (Transform group in root)
            if (group != spawns) SetStaticRecursive(group.gameObject);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ArenaBuilder] Built {w}x{h} tiles ({w * t}x{h * t} units).");
    }

    // ---------- tiles ----------

    static void BuildRoad(Transform parent, ArenaGrid grid, int x, int y, Vector3 p, float t, string id)
    {
        bool n = Connects(grid.Get(x, y - 1)), s = Connects(grid.Get(x, y + 1));
        bool e = Connects(grid.Get(x + 1, y)), wv = Connects(grid.Get(x - 1, y));
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (wv ? 1 : 0);
        string kind = count >= 4 ? "Cross" : count == 3 ? "TJunction" : (n && s) || (e && wv) || count <= 1 ? "Straight" : "Curve";

        var tile = Flat(parent, $"Road_{kind}_{id}", p + Vector3.up * 0.02f, t, t, Mat("Asphalt", new Color(0.22f, 0.23f, 0.25f)));

        // Dashed centre line on straights so you can feel your speed.
        bool vertical = (n || s) && !(e || wv);
        bool horizontal = (e || wv) && !(n || s);
        if (kind == "Straight" && (vertical || horizontal || count == 0))
        {
            var lineMat = Mat("RoadLine", new Color(0.95f, 0.80f, 0.25f));
            for (int i = 0; i < 3; i++)
            {
                float off = (i - 1) * t / 3f;
                Vector3 dp = vertical ? new Vector3(0, 0, off) : new Vector3(off, 0, 0);
                Flat(tile.transform.parent, $"Dash_{id}_{i}", p + dp + Vector3.up * 0.03f,
                    vertical ? 0.35f : t / 6f, vertical ? t / 6f : 0.35f, lineMat);
            }
        }
    }

    static bool Connects(char c) => c == 'R' || c == 'X' || c == 'G' || c == 'P' || c == 'K';

    static void BuildRoundabout(Transform parent, Vector3 p, float t)
    {
        Flat(parent, "Roundabout_Road", p + Vector3.up * 0.02f, t, t, Mat("Asphalt", new Color(0.22f, 0.23f, 0.25f)));
        // Grass island with a low curb.
        var island = Cylinder(parent, "Roundabout_Island", p + Vector3.up * 0.25f, new Vector3(t * 0.75f, 0.25f, t * 0.75f), Mat("Grass", new Color(0.36f, 0.62f, 0.30f)));
        Object.DestroyImmediate(island.GetComponent<Collider>());
        island.AddComponent<MeshCollider>().convex = true;
        // Lucky Tower placeholder landmark.
        Cylinder(parent, "LuckyTower_Placeholder", p + Vector3.up * 9f, new Vector3(3f, 9f, 3f), Mat("Tower", new Color(0.85f, 0.70f, 0.30f)));
        Box(parent, "LuckyTower_Top", p + Vector3.up * 19.5f, new Vector3(4f, 3f, 4f), Mat("Gold", new Color(1f, 0.82f, 0.2f)), false);
    }

    static void BuildPlaza(Transform parent, Vector3 p, float t, string id, System.Random rand)
    {
        var tile = Flat(parent, $"Plaza_{id}", p + Vector3.up * 0.02f, t, t, Mat("Concrete", new Color(0.58f, 0.57f, 0.54f)));
        var stripe = Mat("ParkingLine", new Color(0.92f, 0.92f, 0.88f));
        for (int i = 0; i < 4; i++)
            Flat(tile.transform.parent, $"ParkingLine_{id}_{i}", p + new Vector3((i - 1.5f) * 2.8f, 0.03f, t * 0.32f), 0.2f, 3.2f, stripe);
        // A crate or two for cover.
        int crates = rand.Next(0, 3);
        for (int i = 0; i < crates; i++)
        {
            Vector3 cp = p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.6f, 0.75f, (float)(rand.NextDouble() - 0.5) * t * 0.4f - t * 0.15f);
            Box(parent, $"Crate_{id}_{i}", cp, new Vector3(1.5f, 1.5f, 1.5f), Mat("Crate", new Color(0.62f, 0.45f, 0.25f)));
        }
    }

    static void BuildPark(Transform parent, Vector3 p, float t, string id, System.Random rand)
    {
        Flat(parent, $"Park_{id}", p + Vector3.up * 0.02f, t, t, Mat("Grass", new Color(0.36f, 0.62f, 0.30f)));
        int trees = 2 + rand.Next(0, 2);
        for (int i = 0; i < trees; i++)
        {
            Vector3 tp = p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.7f, 0f, (float)(rand.NextDouble() - 0.5) * t * 0.7f);
            var trunk = Cylinder(parent, $"TreeTrunk_{id}_{i}", tp + Vector3.up * 1.2f, new Vector3(0.8f, 1.2f, 0.8f), Mat("Trunk", new Color(0.42f, 0.28f, 0.18f)));
            var crown = Sphere(parent, $"TreeCrown_{id}_{i}", tp + Vector3.up * 3.6f, Vector3.one * (3.2f + (float)rand.NextDouble()), Mat("Leaves", new Color(0.30f, 0.55f, 0.25f)));
            Object.DestroyImmediate(crown.GetComponent<Collider>());
        }
    }

    static void BuildBlock(Transform parent, ArenaGrid grid, int x, int y, Vector3 p, float t, string id, System.Random rand)
    {
        var slab = Box(parent, $"Sidewalk_{id}", p + Vector3.up * 0.06f, new Vector3(t, 0.12f, t), Mat("Sidewalk", new Color(0.70f, 0.68f, 0.64f)), false);
        slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        bool edge = x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1;
        float inner = t - 3f; // leaves a 1.5-unit sidewalk all round
        int layoutKind = rand.Next(3);
        if (layoutKind == 0)
        {
            MakeBuilding(parent, $"Building_{id}", p, new Vector3(inner, 0, inner), edge, rand);
        }
        else
        {
            bool alongX = layoutKind == 1;
            float half = inner / 2f - 0.3f;
            for (int i = 0; i < 2; i++)
            {
                float off = (i == 0 ? -1 : 1) * (half / 2f + 0.3f);
                Vector3 bp = p + (alongX ? new Vector3(off, 0, 0) : new Vector3(0, 0, off));
                Vector3 size = alongX ? new Vector3(half, 0, inner) : new Vector3(inner, 0, half);
                MakeBuilding(parent, $"Building_{id}_{i}", bp, size, edge, rand);
            }
        }
    }

    static void MakeBuilding(Transform parent, string name, Vector3 basePos, Vector3 footprint, bool edge, System.Random rand)
    {
        float height = edge ? 9f + (float)rand.NextDouble() * 8f : 5f + (float)rand.NextDouble() * 9f;
        Color c = BuildingPalette[rand.Next(BuildingPalette.Length)];
        var b = Box(parent, name, basePos + Vector3.up * (height / 2f + 0.12f), new Vector3(footprint.x, height, footprint.z), Mat("Building_" + ColorKey(c), c));
        // Roof cap + a rooftop box so buildings don't all look like plain bricks.
        var roof = Box(b.transform.parent, name + "_Roof", basePos + Vector3.up * (height + 0.27f), new Vector3(footprint.x + 0.4f, 0.3f, footprint.z + 0.4f), Mat("Roof", new Color(0.45f, 0.42f, 0.40f)), false);
        if (rand.NextDouble() < 0.6)
        {
            Vector3 ac = basePos + new Vector3((float)(rand.NextDouble() - 0.5) * footprint.x * 0.5f, height + 1.0f, (float)(rand.NextDouble() - 0.5) * footprint.z * 0.5f);
            Box(b.transform.parent, name + "_RoofUnit", ac, new Vector3(1.6f, 1.2f, 1.6f), Mat("RoofUnit", new Color(0.65f, 0.66f, 0.66f)), false);
        }
    }

    static void BuildGarage(Transform parent, Transform spawns, ArenaGrid grid, GarageInfo gd, Vector3 p, float t)
    {
        var root = new GameObject("Garage_" + gd.name.Replace(" ", "")).transform;
        root.SetParent(parent, false);
        root.position = p;

        Flat(root, "Floor", p + Vector3.up * 0.02f, t, t, Mat("Concrete", new Color(0.58f, 0.57f, 0.54f)));
        var wallMat = Mat("Garage_" + ColorKey(gd.color), gd.color);
        float wallH = 3f, thick = 0.8f;

        // Walls on the sides that aren't open to the road; open sides get an entry stripe.
        Vector3 openDir = Vector3.zero;
        var dirs = new[] { (dx: 0, dy: -1, v: Vector3.forward), (dx: 0, dy: 1, v: Vector3.back), (dx: 1, dy: 0, v: Vector3.right), (dx: -1, dy: 0, v: Vector3.left) };
        foreach (var d in dirs)
        {
            bool isOpen = ArenaGrid.IsDrivable(grid.Get(gd.x + d.dx, gd.y + d.dy));
            Vector3 side = d.v * (t / 2f - thick / 2f);
            Vector3 size = d.v.x != 0 ? new Vector3(thick, wallH, t) : new Vector3(t, wallH, thick);
            if (isOpen)
            {
                openDir += d.v;
                Flat(root, "EntryStripe", p + d.v * (t / 2f - 0.6f) + Vector3.up * 0.03f, d.v.x != 0 ? 1f : t, d.v.x != 0 ? t : 1f, Mat("Gold", new Color(1f, 0.82f, 0.2f)));
            }
            else
            {
                Box(root, "Wall", p + side + Vector3.up * wallH / 2f, size, wallMat);
            }
        }
        // No roof: the top-down camera must see the car inside. A corner pillar carries the colour.
        Vector3 back = -openDir.normalized * (t / 2f - 1.2f);
        Box(root, "SignPillar", p + back + Vector3.up * 4f, new Vector3(1.6f, 8f, 1.6f), wallMat);

        var sp = new GameObject("Spawn_" + gd.name.Replace(" ", "")).AddComponent<SpawnPoint>();
        sp.transform.SetParent(spawns, false);
        sp.carName = gd.name;
        sp.color = gd.color;
        Vector3 face = openDir.sqrMagnitude > 0 ? openDir.normalized : (-p).normalized;
        sp.transform.SetPositionAndRotation(p - face * 1.5f + Vector3.up * 0.05f, Quaternion.LookRotation(face, Vector3.up));
    }

    static void BuildWalls(Transform parent, float sizeX, float sizeZ)
    {
        var m = Mat("ArenaWall", new Color(0.55f, 0.52f, 0.48f));
        float hgt = 3f, thick = 1f;
        Box(parent, "Wall_North", new Vector3(0, hgt / 2, sizeZ / 2 + thick / 2), new Vector3(sizeX + thick * 2, hgt, thick), m);
        Box(parent, "Wall_South", new Vector3(0, hgt / 2, -sizeZ / 2 - thick / 2), new Vector3(sizeX + thick * 2, hgt, thick), m);
        Box(parent, "Wall_East", new Vector3(sizeX / 2 + thick / 2, hgt / 2, 0), new Vector3(thick, hgt, sizeZ), m);
        Box(parent, "Wall_West", new Vector3(-sizeX / 2 - thick / 2, hgt / 2, 0), new Vector3(thick, hgt, sizeZ), m);
    }

    // ---------- helpers ----------

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

    static GameObject Sphere(Transform parent, string name, Vector3 worldCenter, Vector3 scale, Material m)
        => Prim(PrimitiveType.Sphere, parent, name, worldCenter, scale, m, true);

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

    /// <summary>Flat upward-facing quad, no collider, no shadows.</summary>
    static GameObject Flat(Transform parent, string name, Vector3 worldCenter, float sizeX, float sizeZ, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(worldCenter, Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static Material Mat(string name, Color c)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Graybox");
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.15f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static string ColorKey(Color c) => ColorUtility.ToHtmlStringRGB(c);

    static void SetStaticRecursive(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject);
    }
}
