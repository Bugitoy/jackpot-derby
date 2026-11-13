using UnityEngine;

/// <summary>
/// The arena as a grid of square tiles, written as text (one line per row, top row = north).
/// The editor menu "Jackpot Derby > Build Arena" turns this into geometry, and gameplay
/// scripts use it to find open ground (chest spawns) and roads.
///
/// Legend:  R road   P plaza/parking (open, drivable)   K park (grass + trees)
///          . city block (sidewalk + buildings)   X roundabout island   G garage
/// A road that dead-ends at the edge (middle of each side) ends in a tunnel built into the wall.
/// </summary>
public class ArenaGrid : MonoBehaviour
{
    public const string DefaultLayout =
        "GR......R......RG\n" +
        "RRRRRRRRRRRRRRRRR\n" +
        ".R...R..R..R...R.\n" +
        ".RPPPR.KRK.RPPPR.\n" +
        ".R...R.RRR.R...R.\n" +
        "RRRRRRRRXRRRRRRRR\n" +
        ".R..R..RRR..R..R.\n" +
        ".RPPR..KRK..RPPR.\n" +
        ".R..R...R...R..R.\n" +
        "RRRRRRRRRRRRRRRRR\n" +
        "GR......R......RG";

    public float tileSize = 12f;
    [Tooltip("Random seed for building heights/colours, so rebuilding gives the same city.")]
    public int seed = 7;
    [TextArea(11, 20)] public string layout = DefaultLayout;

    string[] rows;

    public int Width { get { Parse(); return rows[0].Length; } }
    public int Height { get { Parse(); return rows.Length; } }
    public Vector2 SizeInUnits => new Vector2(Width * tileSize, Height * tileSize);

    public void Parse(bool force = false)
    {
        if (rows != null && !force) return;
        rows = layout.Replace("\r", "").Trim().Split('\n');
        for (int i = 0; i < rows.Length; i++) rows[i] = rows[i].Trim();
    }

    void OnValidate() => rows = null;

    /// <summary>Tile character at column x, row y (row 0 = north edge). Outside = wall '#'.</summary>
    public char Get(int x, int y)
    {
        Parse();
        if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length) return '#';
        return rows[y][x];
    }

    public static bool IsDrivable(char c) => c == 'R' || c == 'P' || c == 'K' || c == 'G';
    public static bool IsRoad(char c) => c == 'R';

    /// <summary>World-space centre of a tile (ground level).</summary>
    public Vector3 TileCenter(int x, int y)
    {
        float wx = (x - (Width - 1) * 0.5f) * tileSize;
        float wz = ((Height - 1) * 0.5f - y) * tileSize;
        return transform.position + new Vector3(wx, 0f, wz);
    }

    /// <summary>Which tile a world position falls in.</summary>
    public Vector2Int WorldToTile(Vector3 p)
    {
        p -= transform.position;
        int x = Mathf.RoundToInt(p.x / tileSize + (Width - 1) * 0.5f);
        int y = Mathf.RoundToInt((Height - 1) * 0.5f - p.z / tileSize);
        return new Vector2Int(x, y);
    }
}
