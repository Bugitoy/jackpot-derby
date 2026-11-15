using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the arena from the ArenaGrid text layout using the real models (light prefabs made by
/// SceneryKit): textured roads that line up, sidewalks, buildings facing their street with rooftop
/// dressing, street props and parked cars for cover, parks, plazas, the central island (looped by
/// ordinary road tiles) with a
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
    static ArenaGrid arena;
    /// <summary>Street lamps placed so far (so two never end up side by side).</summary>
    static List<Vector3> lampSpots;
    /// <summary>Which building model went on which tile (inner and outer grid), for variety.</summary>
    static Dictionary<Vector2Int, string> buildingAt;
    const float LampSpacing = 6f;
    /// <summary>Outer tiles behind each garage (the garage model pokes out past the edge into them).</summary>
    static HashSet<Vector2Int> yards;

    [MenuItem("Jackpot Derby/Build Arena")]
    public static void BuildArena()
    {
        mats = new Dictionary<string, Material>();
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) grid = new GameObject("Arena").AddComponent<ArenaGrid>();
        grid.layout = ArenaGrid.DefaultLayout;
        EditorUtility.SetDirty(grid);
        grid.Parse(true);
        arena = grid;
        lampSpots = new List<Vector3>();
        buildingAt = new Dictionary<Vector2Int, string>();
        // The tile just outside each garage's back wall: the garage model stands there.
        yards = new HashSet<Vector2Int>();
        foreach (var gy in new[] { new Vector2Int(0, 0), new Vector2Int(grid.Width - 1, 0), new Vector2Int(0, grid.Height - 1), new Vector2Int(grid.Width - 1, grid.Height - 1) })
            yards.Add(gy.y == 0 ? new Vector2Int(gy.x, -1) : gy.x == 0 ? new Vector2Int(-1, gy.y) : new Vector2Int(grid.Width, gy.y));
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
        // City paving, not grass: everything past the walls is pavement under the outer city and haze.
        float outerSpan = (OuterRings + DeepRings + 2) * t * 2f;
        // Same sidewalk look as everywhere else, tiled at the same scale (one repeat per 6 units).
        Vector3 slabSize = new Vector3(w * t + outerSpan, 1, h * t + outerSpan);
        var groundMat = TexMat("GroundSidewalk", "tex_sidewalk", Color.white, 1f);
        groundMat.SetTextureScale("_BaseMap", new Vector2(slabSize.x / 6f, slabSize.z / 6f));
        var g = Box(ground, "GroundSlab", new Vector3(0, -0.5f, 0), slabSize, groundMat);
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

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            char c = grid.Get(x, y);
            Vector3 p = grid.TileCenter(x, y);
            string id = $"{x}_{y}";
            var key = new Vector2Int(x, y);
            // The centre: an island the 8 road tiles around it loop round (their own road textures
            // make the loop, so every lane lines up with the streets).
            if (c == 'X') { BuildCentreIsland(roads, p, t); continue; }
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

        BuildBoundary(walls, w * t, h * t);
        BuildOuterCity(outside, t);
        TidyRoofsAndMountSigns(root);
        FixRoofPropsForLods(root);
        // Parked cars can land on a bench or hydrant poking off the sidewalk: slide them clear.
        PushableSetup.ResolveOverlaps(grid);

        foreach (var group in new[] { ground, roads })
            SetStaticRecursive(group.gameObject);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ArenaBuilder] Built {w}x{h} tiles ({w * t}x{h * t} units).");
    }

    // ---------- roads ----------

    // Only real road continues a road's painted lanes. Plazas and parks sit beside the road (its edge
    // line runs past them) instead of a lane running into their pavement and stopping dead.
    static bool Connects(char c) => c == 'R' || c == 'G' || c == 'O';

    /// <summary>Lay one road tile with the texture that matches its arms. Returns the texture used.</summary>
    static string RoadSurface(Transform parent, string id, Vector3 p, float t, bool n, bool e, bool s, bool wv, out float yaw)
    {
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (wv ? 1 : 0);
        // Texture orientation (image top = north). Yaw turns N -> E -> S -> W.
        string tex;
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
        return tex;
    }

    static void BuildRoad(Transform parent, Transform props, ArenaGrid grid, int x, int y, Vector3 p, float t, string id)
    {
        // Neighbours come from the extended grid, so roads leaving the arena join the outer streets.
        bool n = Connects(Ext(x, y - 1)), s = Connects(Ext(x, y + 1));
        bool e = Connects(Ext(x + 1, y)), wv = Connects(Ext(x - 1, y));

        string tex = RoadSurface(parent, id, p, t, n, e, s, wv, out float yaw);
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (wv ? 1 : 0);

        // Straight roads get parked cars and clutter on the wide shoulders (cover for dogfights).
        // Not on the edge tiles: roads leaving the arena stay clear up to the road-closed barrier.
        bool edgeTile = x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1;
        if (tex != "tex_road_straight" || count < 2 || edgeTile) return;
        bool alongZ = yaw == 0f;
        Vector3 along = alongZ ? Vector3.forward : Vector3.right;
        Vector3 across = alongZ ? Vector3.right : Vector3.forward;
        for (int side = -1; side <= 1; side += 2)
        {
            double roll = rand.NextDouble();
            Vector3 basePos = p + across * side * (t * 0.36f) + along * (float)(rand.NextDouble() - 0.5) * t * 0.4f;
            float faceYaw = (alongZ ? 0f : 90f) + (rand.Next(2) == 0 ? 0f : 180f);
            if (roll < 0.28) PushableSetup.Make(Place(props, Pick(ShoulderPool), basePos, faceYaw)); // rolls when rammed
            else if (roll < 0.36) Place(props, Pick(ClutterPool), basePos, faceYaw + (float)rand.NextDouble() * 30f);
        }
    }

    /// <summary>
    /// The centre tile: a raised island (sidewalk curb round a lawn) with the giant slot machine.
    /// Cars drive round it on the ring of road tiles; a box collider keeps them off the lawn.
    /// </summary>
    static void BuildCentreIsland(Transform parent, Vector3 p, float t)
    {
        var curb = Box(parent, "CentreIsland_Curb", p + Vector3.up * 0.1f, new Vector3(t * 0.92f, 0.2f, t * 0.92f), SidewalkMat(), false);
        curb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var lawn = Box(parent, "CentreIsland_Lawn", p + Vector3.up * 0.13f, new Vector3(t * 0.8f, 0.22f, t * 0.8f), TexMat("ParkLawn", "tex_grass", new Color(0.62f, 0.78f, 0.62f), 1.5f), false);
        lawn.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var block = new GameObject("CentreIsland_Collider");
        block.transform.SetParent(parent, false);
        block.transform.position = p + Vector3.up * 1.5f;
        block.AddComponent<BoxCollider>().size = new Vector3(t * 0.92f, 3f, t * 0.92f);

        // Landmark: a giant slot machine (stand-in for lucky_tower).
        var slot = Place(parent, "slot_machine", p, 180f);
        if (slot != null) slot.transform.localScale = Vector3.one * 6.5f;
        ScatterGrass(parent, p, t * 0.36f, 10);
        float r = t * 0.36f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? -r : r, 0.24f, i < 2 ? -r : r);
            Place(parent, i % 2 == 0 ? "bush_large" : "potted_plants", p + corner, i * 90f);
        }
    }

    // ---------- open ground ----------

    static void BuildPlaza(Transform parent, Vector3 p, float t, string id)
    {
        Flat(parent, $"Plaza_{id}", p + Vector3.up * 0.02f, t, t, 0f, SidewalkMat());
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

    /// <summary>Height of a park's lawn top: it sits a little above the sidewalk ring, like the centre island.</summary>
    const float ParkLawnTop = 0.16f;
    /// <summary>Lawn width as a share of the tile; the rest is the sidewalk ring round it.</summary>
    const float ParkLawnShare = 0.84f;

    static void BuildPark(Transform parent, Vector3 p, float t, string id)
    {
        // Same look as the centre island: a sidewalk ring (flush with the block sidewalks next to it)
        // round a slightly raised lawn. Visual only, no colliders, so the park stays drivable open ground.
        var walk = Box(parent, $"ParkSidewalk_{id}", p + Vector3.up * 0.06f, new Vector3(t, 0.12f, t), SidewalkMat(), false);
        walk.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        // Soft sage-teal lawn that sits with the palette (the raw grass texture was too loud).
        var lawn = Box(parent, $"Park_{id}", p + Vector3.up * (ParkLawnTop * 0.5f), new Vector3(t * ParkLawnShare, ParkLawnTop, t * ParkLawnShare),
                       TexMat("ParkLawn", "tex_grass", new Color(0.62f, 0.78f, 0.62f), 1.5f), false);
        lawn.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        p += Vector3.up * ParkLawnTop;   // everything below stands on the lawn
        ScatterGrass(parent, p, t * 0.38f, 8);
        Place(parent, "bush_large", p + new Vector3(-t * 0.36f, 0, (float)(rand.NextDouble() - 0.5) * t * 0.5f), (float)rand.NextDouble() * 360f);
        Place(parent, "tree_planter", p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.5f, 0, t * 0.36f), (float)rand.NextDouble() * 360f);
        int trees = 1 + rand.Next(0, 2);
        for (int i = 0; i < trees; i++)
        {
            Vector3 tp = p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.6f, 0f, (float)(rand.NextDouble() - 0.5) * t * 0.6f);
            Place(parent, rand.Next(2) == 0 ? "tree_twisted" : "tree_planter", tp, (float)rand.NextDouble() * 360f);
        }
        if (rand.NextDouble() < 0.6) Place(parent, "park_bench", p + new Vector3((float)(rand.NextDouble() - 0.5) * t * 0.5f, 0, -t * 0.35f), 180f);
        if (rand.NextDouble() < 0.6) Place(parent, "bush_large", p + new Vector3(t * 0.35f, 0, (float)(rand.NextDouble() - 0.5) * t * 0.5f), (float)rand.NextDouble() * 360f);
    }

    /// <summary>
    /// Grass tufts (Lefika's grass_tuft model, if it has been added) scattered over a square patch.
    /// Does nothing until Assets/Models/grass_tuft.glb exists and Build Scenery Prefabs has run.
    /// </summary>
    static void ScatterGrass(Transform parent, Vector3 centre, float halfSize, int count)
    {
        if (SceneryKit.Get("grass_tuft") == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = centre + new Vector3((float)(rand.NextDouble() * 2 - 1) * halfSize, 0.02f, (float)(rand.NextDouble() * 2 - 1) * halfSize);
            var g = Place(parent, "grass_tuft", pos, (float)rand.NextDouble() * 360f, false);
            if (g == null) continue;
            g.transform.localScale = Vector3.one * (0.8f + (float)rand.NextDouble() * 0.4f);
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // ---------- city blocks ----------

    static void BuildBlock(Transform parent, Transform props, ArenaGrid grid, int x, int y, Vector3 p, float t, string id, string landmark)
    {
        var slab = Box(parent, $"Sidewalk_{id}", p + Vector3.up * 0.06f, new Vector3(t, 0.12f, t), SidewalkMat(), false);
        slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        slab.isStatic = true;

        // Face the building toward a street. The camera looks north, so prefer a street to the south.
        var dirs = new[] { (dx: 0, dy: 1, yaw: 180f, v: Vector3.back), (dx: 1, dy: 0, yaw: 90f, v: Vector3.right),
                           (dx: -1, dy: 0, yaw: 270f, v: Vector3.left), (dx: 0, dy: -1, yaw: 0f, v: Vector3.forward) };
        float face = 180f;
        foreach (var d in dirs)
            if (ArenaGrid.IsDrivable(grid.Get(x + d.dx, y + d.dy))) { face = d.yaw; break; }

        bool edge = x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1;
        string model = landmark ?? PickBuilding(new Vector2Int(x, y), BuildingPool);
        if (landmark != null) buildingAt[new Vector2Int(x, y)] = landmark;
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
            if (rand.NextDouble() < 0.6) PlaceLamp(props, edgeCentre + side * t * 0.4f * (rand.Next(2) == 0 ? 1 : -1), d.yaw);
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

    // ---------- tidy roofs, neon signs on walls ----------

    /// <summary>Small rooftop units Lefika asked to drop: too small to notice, and they cluttered roofs.</summary>
    static readonly string[] SmallRoofProps = { "roof_vents", "roof_ac_unit", "roof_antennas" };
    /// <summary>Projecting "blade" signs: the brackets on the model's -X side bolt to a wall.</summary>
    static readonly string[] WallSignPool = { "neon_sign_clover", "neon_sign_dice" };
    const int WallSignCount = 14;
    const float WallSignMinBottom = 4.5f;

    [MenuItem("Jackpot Derby/Tidy Roofs And Mount Neon Signs")]
    static void TidyRoofsMenu()
    {
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) return;
        TidyRoofsAndMountSigns(grid.transform);
        FixRoofPropsForLods(grid.transform);
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
    }

    /// <summary>
    /// Removes the small rooftop props (vents, AC units, antennas) and moves the neon signs off the
    /// roofs onto building fronts, mounted like real shop signs: brackets bolted to a flat stretch of
    /// wall a little below the roofline, the sign sticking out over the sidewalk so traffic reads it.
    /// A spot is used only if the wall behind the brackets is flat and facing the street, nothing of
    /// the facade (balconies, awnings, fire escapes) pokes into the sign, the sign stays clear of
    /// lamps and other props, and it hangs at least WallSignMinBottom above the ground.
    /// </summary>
    public static void TidyRoofsAndMountSigns(Transform arenaRoot)
    {
        var small = new HashSet<string>(SmallRoofProps);
        var wallSigns = new HashSet<string>(WallSignPool);
        int removedSmall = 0, removedSigns = 0;
        foreach (var mf in arenaRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            string n = mf.sharedMesh.name;
            if (small.Contains(n)) { Undo.DestroyObjectImmediate(mf.gameObject); removedSmall++; }
            else if (wallSigns.Contains(n)) { Undo.DestroyObjectImmediate(mf.gameObject); removedSigns++; }
        }

        // Candidate buildings: the arena's own blocks (the streets the player drives).
        var blocks = arenaRoot.Find("Blocks");
        var buildings = new List<MeshFilter>();
        var others = new List<Renderer>();
        foreach (var mf in arenaRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var r = mf.GetComponent<Renderer>();
            bool isBuilding = mf.sharedMesh.lodCount > 1 && r.bounds.size.x >= 6f && r.bounds.size.y >= 5f;
            if (isBuilding && blocks != null && mf.transform.IsChildOf(blocks)) buildings.Add(mf);
            // Street props, lamps, rooftop towers and billboards (not ground slabs or buildings).
            else if (!isBuilding && r.bounds.size.y > 0.5f && Mathf.Max(r.bounds.size.x, r.bounds.size.z) < 6f) others.Add(r);
        }

        var signPrefab = SceneryKit.Get(WallSignPool[0]);
        if (signPrefab == null) return;
        Bounds sb = signPrefab.GetComponent<MeshFilter>().sharedMesh.bounds;
        float reach = -sb.min.x;                          // pivot to the bracket plates
        float bracketY = sb.min.y + sb.size.y * 0.33f;    // between the two bracket arms
        var rng = new System.Random(777);
        // Shuffle so the signs spread over the map.
        for (int i = buildings.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (buildings[i], buildings[j]) = (buildings[j], buildings[i]); }

        var probe = new GameObject("SignProbe") { hideFlags = HideFlags.HideAndDontSave };
        var mc = probe.AddComponent<MeshCollider>();
        var mounted = new List<Vector3>();
        var lodCache = new Dictionary<Mesh, Mesh>();
        int placed = 0;
        try
        {
            foreach (var b in buildings)
            {
                if (placed >= WallSignCount) break;
                if (!lodCache.TryGetValue(b.sharedMesh, out var full)) lodCache[b.sharedMesh] = full = LodMeshes(b.sharedMesh)[0];
                probe.transform.SetPositionAndRotation(b.transform.position, b.transform.rotation);
                probe.transform.localScale = b.transform.lossyScale;
                mc.sharedMesh = full;
                Bounds mb = b.sharedMesh.bounds;
                Bounds wb = b.GetComponent<Renderer>().bounds;
                Vector3 fwd = b.transform.forward, right = b.transform.right;
                bool done = false;
                foreach (float side in rng.Next(2) == 0 ? new[] { 1f, -1f } : new[] { -1f, 1f })
                {
                    for (float h = 0.72f; h >= 0.45f && !done; h -= 0.09f)
                    {
                        float pivotY = wb.min.y + wb.size.y * h - sb.size.y * 0.5f;
                        if (pivotY + sb.min.y < wb.min.y + WallSignMinBottom) continue;
                        if (pivotY + sb.max.y > wb.max.y - 0.6f) continue;
                        // Find the wall behind the brackets.
                        Vector3 lateral = b.transform.TransformPoint(new Vector3(mb.center.x + side * 0.3f * mb.size.x, mb.center.y, mb.max.z));
                        Vector3 origin = new Vector3(lateral.x, pivotY + bracketY, lateral.z) + fwd * 4f;
                        if (!mc.Raycast(new Ray(origin, -fwd), out RaycastHit wall, 12f)) continue;
                        if (Vector3.Dot(wall.normal, fwd) < 0.75f) continue;
                        // The sign's slab, from the wall out: nothing of the facade may stick into it,
                        // and there must be wall behind all of it (not sky above the roofline).
                        bool clear = true;
                        foreach (float dy in new[] { 0.15f, 0.5f, 0.85f })
                            foreach (float dl in new[] { -0.9f, -0.45f, 0f, 0.45f, 0.9f }) // the sign plus room either side
                            {
                                Vector3 o = new Vector3(wall.point.x, pivotY + sb.min.y + sb.size.y * dy, wall.point.z) + right * dl + fwd * 4f;
                                if (!mc.Raycast(new Ray(o, -fwd), out RaycastHit h2, 12f)) { clear = false; break; }
                                if (Vector3.Dot(h2.point - wall.point, fwd) > 0.3f) { clear = false; break; }
                            }
                        if (!clear) continue;
                        // Face along the street: the model's -X (brackets) then points at the wall.
                        Vector3 faceDir = -right;
                        Vector3 pivot = new Vector3(wall.point.x, pivotY, wall.point.z) + fwd * (reach - 0.02f);
                        var signBounds = new Bounds(pivot + Vector3.up * sb.center.y, new Vector3(1f, 0f, 1f) * (sb.size.x + 0.4f) + Vector3.up * sb.size.y);
                        bool hits = false;
                        foreach (var o in others) if (o != null && o.bounds.Intersects(signBounds)) { hits = true; break; }
                        if (hits) continue;
                        bool near = false;
                        foreach (var m in mounted) if ((m - pivot).sqrMagnitude < 12f * 12f) { near = true; break; }
                        if (near) continue;
                        float yaw = Quaternion.LookRotation(faceDir).eulerAngles.y;
                        var sign = Place(b.transform.parent, WallSignPool[placed % WallSignPool.Length], pivot, yaw, false);
                        if (sign == null) continue;
                        Undo.RegisterCreatedObjectUndo(sign, "Mount neon sign");
                        mounted.Add(pivot);
                        placed++;
                        done = true;
                    }
                    if (done) break;
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(probe);
            foreach (var m in lodCache.Values) Object.DestroyImmediate(m);
        }
        Debug.Log($"[ArenaBuilder] Roofs tidied: {removedSmall} small roof props removed, {removedSigns} roof neon signs taken down, {placed} neon signs mounted on walls.");
    }

    // ---------- roof props: whole base on flat roof, steady across Mesh LODs ----------

    /// <summary>How far (units) the roof under a prop's base may vary: across the base, and between LODs.</summary>
    const float RoofFlatTolerance = 0.25f;
    const float RoofLodTolerance = 0.3f;

    [MenuItem("Jackpot Derby/Fix Roof Props")]
    static void FixRoofPropsMenu()
    {
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) return;
        FixRoofPropsForLods(grid.transform);
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
    }

    /// <summary>
    /// Makes every rooftop prop (AC units, vents, antennas, water towers, billboards, neon signs) stand
    /// properly on its roof:
    ///  * its WHOLE base rests on flat roof at one height: no legs hanging off the edge, no balancing
    ///    on a chimney, ridge or sloped tiles, nothing of the building poking up through it
    ///    (DressRoof only ever checked the single point under the prop's centre);
    ///  * that roof stays put on every lighter Mesh LOD of the building (simplified roofs can sink or
    ///    rise by several units on ledges, which left props floating a few blocks away);
    ///  * it doesn't overlap another rooftop prop.
    /// A prop that fails is moved to a spot on the same roof that passes, or deleted if there's none.
    /// Uses its own random numbers so the rest of the arena stays as built. Run by Build Arena.
    /// </summary>
    public static void FixRoofPropsForLods(Transform arenaRoot)
    {
        var roofNames = new HashSet<string>(RoofPool);
        var signNames = new HashSet<string>(SignPool);
        roofNames.UnionWith(signNames);
        roofNames.ExceptWith(WallSignPool);   // those hang on walls (TidyRoofsAndMountSigns)
        roofNames.ExceptWith(SmallRoofProps); // removed
        var buildings = new List<MeshFilter>();
        var items = new List<MeshFilter>();
        foreach (var mf in arenaRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            if (roofNames.Contains(mf.sharedMesh.name)) items.Add(mf);
            else if (mf.sharedMesh.lodCount > 1 && mf.GetComponent<Renderer>().bounds.size.x >= 6f) buildings.Add(mf);
        }

        var lodCache = new Dictionary<Mesh, Mesh[]>();
        var probe = new GameObject("RoofProbe") { hideFlags = HideFlags.HideAndDontSave };
        var mc = probe.AddComponent<MeshCollider>();
        var rng = new System.Random(4242);
        int moved = 0, removed = 0;
        // Footprints of the props kept so far, so none end up overlapping.
        var placed = new List<(Vector3 pos, float radius)>();
        bool Free(Vector3 at, float r)
        {
            foreach (var (pos, radius) in placed)
                if (Mathf.Abs(pos.y - at.y) < 3f && new Vector2(pos.x - at.x, pos.z - at.z).magnitude < radius + r) return false;
            return true;
        }
        try
        {
            foreach (var item in items)
            {
                Vector3 p = item.transform.position;
                MeshFilter bld = null;
                foreach (var b in buildings)
                {
                    Bounds bb = b.GetComponent<Renderer>().bounds;
                    if (p.x > bb.min.x && p.x < bb.max.x && p.z > bb.min.z && p.z < bb.max.z && Mathf.Abs(bb.max.y - p.y) < 8f) { bld = b; break; }
                }
                if (bld == null) { Undo.DestroyObjectImmediate(item.gameObject); removed++; continue; }
                if (!lodCache.TryGetValue(bld.sharedMesh, out var lods)) lodCache[bld.sharedMesh] = lods = LodMeshes(bld.sharedMesh);
                probe.transform.SetPositionAndRotation(bld.transform.position, bld.transform.rotation);
                probe.transform.localScale = bld.transform.lossyScale;
                Bounds wb = bld.GetComponent<Renderer>().bounds;
                float top = wb.max.y + 5f;
                Vector3 size = Vector3.Scale(item.sharedMesh.bounds.size, item.transform.lossyScale);
                float spacing = Mathf.Max(size.x, size.z) * 0.5f;
                Quaternion rot = item.transform.rotation;

                if (RoofSpotOk(mc, lods, item, p, rot, top, out float y) && Free(p, spacing))
                {
                    if (Mathf.Abs(y - p.y) > 0.01f)
                    {
                        Undo.RecordObject(item.transform, "Fix roof prop");
                        item.transform.position = new Vector3(p.x, y, p.z);
                    }
                    placed.Add((item.transform.position, spacing));
                    continue;
                }

                // Look for a spot on the same roof where the whole base fits. Signs keep facing the
                // street; units and towers may turn.
                Bounds mb = bld.sharedMesh.bounds;
                bool sign = signNames.Contains(item.sharedMesh.name);
                bool found = false;
                for (int attempt = 0; attempt < 150 && !found; attempt++)
                {
                    Vector3 local = new Vector3((float)(rng.NextDouble() - 0.5) * 0.8f, 0, (float)(rng.NextDouble() - 0.5) * 0.8f);
                    Vector3 o = bld.transform.TransformPoint(mb.center + Vector3.Scale(local, mb.size));
                    mc.sharedMesh = lods[0];
                    if (!mc.Raycast(new Ray(new Vector3(o.x, top, o.z), Vector3.down), out RaycastHit hit, top - wb.min.y + 1f)) continue;
                    if (hit.normal.y < 0.96f || hit.point.y < wb.min.y + wb.size.y * 0.55f) continue;
                    Quaternion r = sign ? rot : Quaternion.Euler(0f, rng.Next(4) * 90f + rot.eulerAngles.y, 0f);
                    if (!Free(hit.point, spacing) || !RoofSpotOk(mc, lods, item, hit.point, r, top, out float fy)) continue;
                    Undo.RecordObject(item.transform, "Fix roof prop");
                    item.transform.SetPositionAndRotation(new Vector3(hit.point.x, fy, hit.point.z), r);
                    placed.Add((item.transform.position, spacing));
                    found = true;
                }
                if (found) moved++;
                else { Undo.DestroyObjectImmediate(item.gameObject); removed++; }
            }
        }
        finally
        {
            Object.DestroyImmediate(probe);
            foreach (var kv in lodCache) foreach (var m in kv.Value) Object.DestroyImmediate(m);
        }
        Debug.Log($"[ArenaBuilder] Roof props checked: {items.Count} props, {moved} moved, {removed} removed.");
    }

    /// <summary>
    /// Points under a prop's base: a 4 x 4 grid over its footprint (slightly inset), in world space.
    /// </summary>
    static List<Vector3> BasePoints(MeshFilter item, Vector3 pos, Quaternion rot)
    {
        Bounds b = item.sharedMesh.bounds;
        Vector3 s = item.transform.lossyScale;
        var pts = new List<Vector3>();
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                float u = Mathf.Lerp(-0.45f, 0.45f, i / 3f), v = Mathf.Lerp(-0.45f, 0.45f, j / 3f);
                Vector3 local = new Vector3((b.center.x + u * b.size.x) * s.x, 0f, (b.center.z + v * b.size.z) * s.z);
                pts.Add(pos + rot * local);
            }
        return pts;
    }

    /// <summary>
    /// The prop's whole base lies on flat, level roof (first surface hit from above everywhere under
    /// it, facing up, all within tolerance), and that roof holds on every Mesh LOD. y = where to stand it.
    /// </summary>
    static bool RoofSpotOk(MeshCollider mc, Mesh[] lods, MeshFilter item, Vector3 pos, Quaternion rot, float top, out float y)
    {
        y = pos.y;
        var pts = BasePoints(item, pos, rot);
        var baseY = new float[pts.Count];
        mc.sharedMesh = lods[0];
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < pts.Count; i++)
        {
            if (!mc.Raycast(new Ray(new Vector3(pts[i].x, top, pts[i].z), Vector3.down), out RaycastHit hit, 200f)) return false;
            if (hit.normal.y < 0.94f) return false;
            baseY[i] = hit.point.y;
            lo = Mathf.Min(lo, baseY[i]);
            hi = Mathf.Max(hi, baseY[i]);
        }
        if (hi - lo > RoofFlatTolerance) return false;
        y = hi;
        for (int lod = 1; lod < lods.Length; lod++)
        {
            mc.sharedMesh = lods[lod];
            for (int i = 0; i < pts.Count; i++)
            {
                float ly = mc.Raycast(new Ray(new Vector3(pts[i].x, top, pts[i].z), Vector3.down), out RaycastHit hit, 200f) ? hit.point.y : -99f;
                if (Mathf.Abs(ly - baseY[i]) > RoofLodTolerance) return false;
            }
        }
        return true;
    }

    /// <summary>One plain mesh per Mesh LOD level (same vertices, that level's triangles) for collision tests.</summary>
    static Mesh[] LodMeshes(Mesh mesh)
    {
        var md = Mesh.AcquireReadOnlyMeshData(mesh);
        int[] ib;
        try
        {
            var data = md[0];
            if (data.indexFormat == IndexFormat.UInt16)
            {
                var na = data.GetIndexData<ushort>();
                ib = new int[na.Length];
                for (int i = 0; i < na.Length; i++) ib[i] = na[i];
            }
            else ib = data.GetIndexData<int>().ToArray();
        }
        finally { md.Dispose(); }
        var verts = mesh.vertices;
        var result = new Mesh[Mathf.Max(1, mesh.lodCount)];
        for (int lod = 0; lod < result.Length; lod++)
        {
            var tris = new List<int>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var r = mesh.GetLod(s, lod);
                int bv = mesh.GetSubMesh(s).baseVertex;
                for (int i = 0; i < r.indexCount; i++) tris.Add(ib[r.indexStart + i] + bv);
            }
            var m = new Mesh { indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            result[lod] = m;
        }
        return result;
    }

    // ---------- garages ----------

    static void BuildGarage(Transform parent, Transform spawns, ArenaGrid grid, GarageInfo gd, Vector3 p, float t)
    {
        var root = new GameObject("Garage_" + gd.name.Replace(" ", "")).transform;
        root.SetParent(parent, false);
        root.position = p;

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

        // The garage tile is a real road: its lanes join the neighbouring roads and run on into the
        // garage door, so the corner reads as a driveway instead of a patch of pavement.
        Vector3 back = -doorDir;
        bool n = Connects(Ext(gd.x, gd.y - 1)) || back == Vector3.forward;
        bool s = Connects(Ext(gd.x, gd.y + 1)) || back == Vector3.back;
        bool e = Connects(Ext(gd.x + 1, gd.y)) || back == Vector3.right;
        bool wv = Connects(Ext(gd.x - 1, gd.y)) || back == Vector3.left;
        RoadSurface(root, "Garage", p, t, n, e, s, wv, out _);

        var sp = new GameObject("Spawn_" + gd.name.Replace(" ", "")).AddComponent<SpawnPoint>();
        sp.transform.SetParent(spawns, false);
        sp.carName = gd.name;
        sp.color = gd.color;
        sp.transform.SetPositionAndRotation(p + doorDir * 1.2f + Vector3.up * 0.05f, Quaternion.LookRotation(face, Vector3.up));
    }

    // ---------- edge ----------

    /// <summary>
    /// The edge of the drivable area, with no wall: invisible colliders, and where a road or garage
    /// tile meets the edge, a guardrail run (or a "road closed" barrier where the road carries on
    /// into the outer city). Elsewhere the backs of the edge buildings meet the outer city.
    /// </summary>
    static void BuildBoundary(Transform parent, float sizeX, float sizeZ)
    {
        var m = Mat("ArenaWall", new Color(0.55f, 0.52f, 0.48f));
        float hgt = 12f, thick = 1f;
        var colliders = new[]
        {
            Box(parent, "Edge_North", new Vector3(0, hgt / 2, sizeZ / 2 + thick / 2), new Vector3(sizeX + thick * 2, hgt, thick), m),
            Box(parent, "Edge_South", new Vector3(0, hgt / 2, -sizeZ / 2 - thick / 2), new Vector3(sizeX + thick * 2, hgt, thick), m),
            Box(parent, "Edge_East", new Vector3(sizeX / 2 + thick / 2, hgt / 2, 0), new Vector3(thick, hgt, sizeZ), m),
            Box(parent, "Edge_West", new Vector3(-sizeX / 2 - thick / 2, hgt / 2, 0), new Vector3(thick, hgt, sizeZ), m),
        };
        foreach (var c in colliders) c.GetComponent<Renderer>().enabled = false;

        int w = arena.Width, h = arena.Height;
        float t = arena.tileSize;
        var dirs = new[] { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            char c = arena.Get(x, y);
            if (c != 'R' && c != 'G') continue;
            foreach (var d in dirs)
            {
                int nx = x + d.x, ny = y + d.y;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h) continue;      // not an edge side
                char o = Ext(nx, ny);
                if (o == 'Y') continue;                                     // the garage itself stands there
                Vector3 outward = new Vector3(d.x, 0f, -d.y);
                Vector3 edge = arena.TileCenter(x, y) + outward * (t * 0.5f);
                if (o == 'O') RoadClosed(parent, edge, -outward);
                else GuardrailRun(parent, edge, -outward, t);
            }
        }
    }

    /// <summary>Guardrails along one tile side at the edge, facing into the arena.</summary>
    static void GuardrailRun(Transform parent, Vector3 edge, Vector3 inward, float len)
    {
        float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
        Vector3 along = new Vector3(inward.z, 0f, -inward.x);
        for (int i = -1; i <= 1; i++)
            Place(parent, "guardrail", edge - inward * 0.3f + along * (i * len / 3f), yaw, false);
    }

    /// <summary>Where an arena road carries on into the outer city: barriers and cones across it.</summary>
    static void RoadClosed(Transform parent, Vector3 edge, Vector3 inward)
    {
        float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
        Vector3 side = new Vector3(inward.z, 0f, -inward.x);
        // The barrier model's long side is its Z axis: turn it 90° so it spans the road.
        for (int i = -1; i <= 1; i++)
            Place(parent, "barrier", edge - inward * 0.2f + side * (i * 2.7f), yaw + 90f, false);
        Place(parent, "cone", edge + inward * 1.2f + side * 4.4f, yaw, false);
        Place(parent, "cone", edge + inward * 1.2f - side * 4.4f, yaw + 30f, false);
    }

    // ---------- outer city ----------

    /// <summary>How many tile rings of scenery city surround the arena.</summary>
    const int OuterRings = 5;
    // Outer city: every building type (plus a few landmarks) so neighbours can always differ; the
    // tight edge ring leans toward taller apartments but still mixes in shops and houses.
    static readonly string[] OuterPool =
    {
        "apartment_brick", "apartment_cream", "apartment_cream_2", "apartment_teal",
        "store_cafe", "store_grocer", "store_laundromat", "store_mechanic", "store_pawn",
        "house_small", "house_small_yellow", "house_tall", "garage_small", "arcade_building", "charm_shop",
    };
    static readonly string[] EdgePool =
    {
        "apartment_brick", "apartment_cream", "apartment_cream_2", "apartment_teal", "house_tall",
        "store_grocer", "store_pawn", "store_cafe", "store_laundromat", "house_small", "arcade_building",
    };

    /// <summary>
    /// The tile at (x, y) on a grid that extends past the arena. Inside: the arena layout. Outside, in
    /// rings that get tighter toward the edge of the world:
    ///   ring 1 blocks (B) like the arena's   ring 2 road (O)   ring 3 blocks   ring 4 road
    ///   ring 5 tight lots (D), with tunnels (T) at the end of the streets leaving the arena.
    /// Streets (O) cross rings 1 and 3, carrying on from the arena's mid roads; Y = garage yard.
    /// </summary>
    static char Ext(int x, int y)
    {
        int w = arena.Width, h = arena.Height;
        if (x >= 0 && y >= 0 && x < w && y < h) return arena.Get(x, y);
        int dx = x < 0 ? -x : x >= w ? x - w + 1 : 0;
        int dy = y < 0 ? -y : y >= h ? y - h + 1 : 0;
        int r = Mathf.Max(dx, dy);
        if (r > OuterRings) return '#';
        bool ns = dx == 0, ew = dy == 0; // straight out from a side (not a corner)
        int midX = w / 2, midY = h / 2;
        if (r == 2 || r == 4) return 'O';
        if (r == 5) return (ns && x == midX) || (ew && y == midY) ? 'T' : 'D';
        if ((ns && x == midX) || (ew && y == midY)) return 'O';
        if (r == 3 && ((ns && (x == 3 || x == w - 4)) || (ew && (y == 2 || y == h - 3)))) return 'O';
        if (r == 1 && yards.Contains(new Vector2Int(x, y))) return 'Y';
        return 'B';
    }

    /// <summary>Direction from an outer tile back toward the arena (the way its buildings face).</summary>
    static Vector3 Inward(int x, int y)
    {
        int w = arena.Width, h = arena.Height;
        int dx = x < 0 ? -x : x >= w ? x - w + 1 : 0;
        int dy = y < 0 ? -y : y >= h ? y - h + 1 : 0;
        if (dy >= dx) return y < 0 ? Vector3.back : Vector3.forward;
        return x < 0 ? Vector3.right : Vector3.left;
    }

    /// <summary>
    /// Scenery city around the arena (not drivable: the edge colliders keep cars in). Spacing tightens
    /// ring by ring: blocks with ~1.6-unit gaps and streets, then ~1-unit gaps, then lots packed
    /// shoulder to shoulder, while heights rise only a little. Buildings face the arena; they keep
    /// their box colliders so the chase camera can't swing inside them.
    /// </summary>
    static void BuildOuterCity(Transform parent, float t)
    {
        int w = arena.Width, h = arena.Height;
        var roads = Group(parent, "OuterRoads");
        var lots = Group(parent, "OuterLots");
        var walkway = SidewalkMat();
        for (int y = -OuterRings; y < h + OuterRings; y++)
        for (int x = -OuterRings; x < w + OuterRings; x++)
        {
            char c = Ext(x, y);
            if (c == '#' || (x >= 0 && y >= 0 && x < w && y < h)) continue;
            Vector3 p = arena.TileCenter(x, y);
            string id = $"o{x}_{y}";
            int dx = x < 0 ? -x : x >= w ? x - w + 1 : 0;
            int dy = y < 0 ? -y : y >= h ? y - h + 1 : 0;
            int ring = Mathf.Max(dx, dy);
            Vector3 inward = Inward(x, y);

            if (c == 'O')
            {
                bool n = Connects(Ext(x, y - 1)) || Ext(x, y - 1) == 'T', s = Connects(Ext(x, y + 1)) || Ext(x, y + 1) == 'T';
                bool e = Connects(Ext(x + 1, y)) || Ext(x + 1, y) == 'T', wv = Connects(Ext(x - 1, y)) || Ext(x - 1, y) == 'T';
                string tex = RoadSurface(roads, id, p, t, n, e, s, wv, out float yaw);
                // A few parked cars so the outer streets look lived in.
                if (tex == "tex_road_straight" && rand.NextDouble() < 0.25)
                {
                    Vector3 across = yaw == 0f ? Vector3.right : Vector3.forward;
                    Place(lots, Pick(ShoulderPool), p + across * (t * 0.36f) * (rand.Next(2) == 0 ? 1 : -1), yaw + (rand.Next(2) == 0 ? 0f : 180f), false);
                }
                continue;
            }

            var slab = Box(lots, $"Lot_{id}", p + Vector3.up * 0.06f, new Vector3(t, 0.12f, t), walkway, false);
            slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            slab.isStatic = true;
            float faceYaw = Quaternion.LookRotation(inward).eulerAngles.y;

            if (c == 'Y') continue; // the garage model stands here
            if (c == 'T') { BuildTunnel(lots, p + inward * (t * 0.5f), inward); continue; }

            // Fill and height by ring: gaps close up and roofs rise gently toward the edge.
            float fill = ring == 1 ? 10.3f : ring == 3 ? 11.0f : 11.7f;
            float tall = ring == 1 ? 0.9f + (float)rand.NextDouble() * 0.2f
                       : ring == 3 ? 1.0f + (float)rand.NextDouble() * 0.25f
                       : 1.15f + (float)rand.NextDouble() * 0.3f;
            string model = PickBuilding(new Vector2Int(x, y), ring >= 5 ? EdgePool : OuterPool);
            var b = Place(lots, model, p, faceYaw, true);
            if (b == null) continue;
            Vector3 ms = b.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            float sc = (fill + (float)rand.NextDouble() * 0.3f) / Mathf.Max(ms.x, ms.z);
            b.transform.localScale = new Vector3(sc, sc * tall, sc);
            var br = b.GetComponent<Renderer>();
            if (ring > 1) br.shadowCastingMode = ShadowCastingMode.Off;
            if (ring == 1 && rand.NextDouble() < 0.4) DressRoof(lots, b, faceYaw);

            // A street lamp on the pavement where a block faces a road.
            if (ring <= 3 && Connects(Ext(x + (int)inward.x, y - (int)inward.z)) && rand.NextDouble() < 0.45)
            {
                Vector3 side = new Vector3(inward.z, 0f, -inward.x);
                PlaceLamp(lots, p + inward * (t * 0.5f - 0.45f) + side * t * 0.38f * (rand.Next(2) == 0 ? 1 : -1), faceYaw, false);
            }
        }
        SetStaticRecursive(roads.gameObject);
        BuildDeepRings(lots, t);
        ApplyFarLodBias(parent);
    }

    /// <summary>Mesh LOD selection bias for scenery you can never drive near (positive = lighter sooner).</summary>
    const float FarLodBias = 1.5f;

    /// <summary>
    /// Everything more than two tiles past the arena edge (outer ring 3 onwards) switches to its
    /// lighter Mesh LODs sooner: it's only ever seen from a distance.
    /// </summary>
    public static void ApplyFarLodBias(Transform outside)
    {
        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        float halfX = grid.Width * grid.tileSize * 0.5f, halfZ = grid.Height * grid.tileSize * 0.5f;
        foreach (var r in outside.GetComponentsInChildren<MeshRenderer>())
        {
            Vector3 p = r.bounds.center;
            float beyond = Mathf.Max(Mathf.Abs(p.x) - halfX, Mathf.Abs(p.z) - halfZ);
            if (beyond > grid.tileSize * 2f) r.meshLodSelectionBias = FarLodBias;
        }
    }

    /// <summary>Extra rows of buildings beyond the tight lots, so the city edge has depth.</summary>
    const int DeepRings = 2;

    /// <summary>
    /// Rows past the last ring, staggered so the gaps never line up: every other row is shifted
    /// half a lot sideways, and each building is nudged left/right, back/forward and turned slightly.
    /// Looking down a gap between two buildings, you see the middle of a building behind it.
    /// No colliders or shadows (nothing can get out there).
    /// </summary>
    static void BuildDeepRings(Transform parent, float t)
    {
        int w = arena.Width, h = arena.Height;
        for (int ring = OuterRings + 1; ring <= OuterRings + DeepRings; ring++)
        {
            float stagger = (ring - OuterRings) % 2 == 1 ? t * 0.5f : 0f;
            for (int y = -ring; y < h + ring; y++)
            for (int x = -ring; x < w + ring; x++)
            {
                int dx = x < 0 ? -x : x >= w ? x - w + 1 : 0;
                int dy = y < 0 ? -y : y >= h ? y - h + 1 : 0;
                if (Mathf.Max(dx, dy) != ring) continue;
                Vector3 inward = Inward(x, y);
                Vector3 along = new Vector3(inward.z, 0f, -inward.x);
                float sideJitter = ((float)rand.NextDouble() - 0.5f) * 2.4f;
                float depthJitter = ((float)rand.NextDouble() - 0.5f) * 2.4f;
                Vector3 p = arena.TileCenter(x, y) + along * (stagger + sideJitter) - inward * depthJitter;

                float yaw = Quaternion.LookRotation(inward).eulerAngles.y + ((float)rand.NextDouble() - 0.5f) * 14f;
                var b = Place(parent, PickBuilding(new Vector2Int(x, y), EdgePool), p, yaw, false);
                if (b == null) continue;
                Vector3 ms = b.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                float sc = (11.0f + (float)rand.NextDouble() * 0.6f) / Mathf.Max(ms.x, ms.z);
                float tall = 1.2f + (ring - OuterRings) * 0.1f + (float)rand.NextDouble() * 0.35f;
                b.transform.localScale = new Vector3(sc, sc * tall, sc);
                b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    /// <summary>
    /// A tunnel at the end of an outer street, its mouth facing the arena so it can be seen down the
    /// road. A barrier and cones sit across the mouth.
    /// </summary>
    static void BuildTunnel(Transform parent, Vector3 mouth, Vector3 inward)
    {
        var root = Group(parent, "Tunnel");
        root.position = mouth;
        var prefab = SceneryKit.Get("tunnel_entrance");
        float depth = prefab != null ? prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.z : 6f;
        float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
        Place(root, "tunnel_entrance", mouth - inward * (depth * 0.5f), yaw, false);
        Vector3 side = new Vector3(inward.z, 0f, -inward.x);
        // The barrier model's long side is its Z axis: turn it 90° so it spans the mouth.
        Place(root, "barrier", mouth - inward * 1.5f + side * 1.4f, yaw + 90f, false);
        Place(root, "barrier", mouth - inward * 1.5f - side * 1.4f, yaw + 90f, false);
        Place(root, "cone", mouth + inward * 0.8f + side * 2.2f, yaw, false);
        Place(root, "cone", mouth + inward * 0.8f - side * 2.2f, yaw + 30f, false);
    }

    // ---------- helpers ----------

    static string Pick(string[] pool) => pool[rand.Next(pool.Length)];

    /// <summary>
    /// A building for a tile that differs from every building on the 8 tiles around it (falls back to
    /// the 4 side neighbours, then anything, if the pool is too small). Remembers the choice.
    /// </summary>
    static string PickBuilding(Vector2Int tile, string[] pool)
    {
        string pick = null;
        for (int pass = 0; pass < 3 && pick == null; pass++)
        {
            var near = new HashSet<string>();
            if (pass < 2)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || (pass == 1 && dx != 0 && dy != 0)) continue;
                    if (buildingAt.TryGetValue(new Vector2Int(tile.x + dx, tile.y + dy), out var m)) near.Add(m);
                }
            var options = new List<string>();
            foreach (var m in pool) if (!near.Contains(m)) options.Add(m);
            if (options.Count > 0) pick = options[rand.Next(options.Count)];
        }
        buildingAt[tile] = pick;
        return pick;
    }

    /// <summary>A street lamp, unless another one already stands within a few metres.</summary>
    static void PlaceLamp(Transform parent, Vector3 pos, float yaw, bool keepCollider = true)
    {
        foreach (var l in lampSpots)
            if ((l - pos).sqrMagnitude < LampSpacing * LampSpacing) return;
        if (Place(parent, "street_lamp", pos, yaw, keepCollider) != null) lampSpots.Add(pos);
    }

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

    /// <summary>The one sidewalk look used for every paved surface (blocks, plazas, outer lots).</summary>
    static Material SidewalkMat() => TexMat("Sidewalk", "tex_sidewalk", Color.white, 2f);

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

    /// <summary>Ground textures: full 2K, high-quality compression (no crunch); clamp the road tiles so seams line up.</summary>
    static void ConfigureGroundTextures()
    {
        foreach (var n in new[] { "tex_road_straight", "tex_road_intersection", "tex_road_tjunction", "tex_road_curve", "tex_roundabout", "tex_sidewalk", "tex_grass" })
        {
            var imp = AssetImporter.GetAtPath($"Assets/Textures/{n}.jpg") as TextureImporter;
            if (imp == null) continue;
            var wrap = n.StartsWith("tex_road") || n == "tex_roundabout" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (imp.maxTextureSize == 2048 && imp.wrapMode == wrap && !imp.crunchedCompression
                && imp.textureCompression == TextureImporterCompression.CompressedHQ) continue;
            imp.maxTextureSize = 2048;
            imp.wrapMode = wrap;
            imp.anisoLevel = 8;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.crunchedCompression = false;
            imp.compressionQuality = 100;
            imp.SaveAndReimport();
        }
    }

    /// <summary>
    /// Static-batch the flat ground pieces (road quads, slabs). Scenery models are left out: a
    /// static-batched copy always draws its full-detail Mesh LOD and adds its whole mesh to the build.
    /// </summary>
    static void SetStaticRecursive(GameObject go)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(go)) return;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject);
    }
}
