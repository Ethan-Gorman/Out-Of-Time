using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Wraps the painted floor in a raised greyscale wall, and fills the space beyond it with
// dimmed greyscale tiles so the camera never sees the edge of the world.
// Part of the LevelBorder prefab (nested in LevelEssentials). Nothing it builds is saved into
// the scene: it rebuilds on load, and live in the editor when the floor is painted,
// so it never causes merge conflicts.
[ExecuteAlways]
public class LevelBorder : MonoBehaviour
{
    public Tilemap floor;

    [Header("Wall")]
    public int wallThickness = 3;
    public float wallHeight = 1f;

    public TileBase[] wallTiles;
    public Material wallTopMaterial;
    public Material wallSideMaterial;

    [Header("Outside")]
    public int outsideMargin = 25;

    public TileBase[] outsideTiles;
    public Material outsideMaterial;

    public int seed = 1;

    [Header("Floor")]
    public Material floorMaterial;

    const string RootName = "LevelBorder (generated)";

    GameObject root;
    Grid sourceGrid;
#if UNITY_EDITOR
    bool rebuildQueued;
#endif

    void OnEnable()
    {
        Build();
#if UNITY_EDITOR
        Tilemap.tilemapTileChanged += OnTilesChanged;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        Tilemap.tilemapTileChanged -= OnTilesChanged;
#endif
        Clear();
    }

#if UNITY_EDITOR
    // Inspector edits: rebuild once Unity has finished applying them
    void OnValidate()
    {
        QueueRebuild();
    }

    // Floor painted in the editor: rebuild to fit the new shape
    void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
    {
        if (!Application.isPlaying && map == FindFloor())
            QueueRebuild();
    }

    void QueueRebuild()
    {
        if (rebuildQueued)
            return;

        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (this != null && isActiveAndEnabled)
                Build();
        };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Build()
    {
        Clear();

        var map = FindFloor();
        if (map == null)
            return;

        map.CompressBounds();
        BoundsInt b = map.cellBounds;
        if (b.size.x == 0 || b.size.y == 0)
            return;

        sourceGrid = map.layoutGrid;
        float floorY = map.transform.position.y;

        if (Application.isPlaying && floorMaterial != null)
            map.GetComponent<TilemapRenderer>().sharedMaterial = floorMaterial;

        // Our own Grid, matching the floor's, so generated tiles line up exactly
        root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        root.transform.SetPositionAndRotation(sourceGrid.transform.position, sourceGrid.transform.rotation);
        var grid = root.AddComponent<Grid>();
        grid.cellSize = sourceGrid.cellSize;
        grid.cellGap = sourceGrid.cellGap;
        grid.cellLayout = sourceGrid.cellLayout;
        grid.cellSwizzle = sourceGrid.cellSwizzle;

        // Sort every cell around the floor into outside, or one of the four wall sides.
        // South and north run the full width (corners included); west and east fill in between
        int t = wallThickness, m = outsideMargin;
        var outsideCells = new List<Vector3Int>();
        var sides = new[] { new List<Vector3Int>(), new List<Vector3Int>(), new List<Vector3Int>(), new List<Vector3Int>() };
        for (int x = b.xMin - t - m; x < b.xMax + t + m; x++)
        {
            for (int y = b.yMin - t - m; y < b.yMax + t + m; y++)
            {
                bool inside = x >= b.xMin && x < b.xMax && y >= b.yMin && y < b.yMax;
                if (inside)
                    continue;

                var cell = new Vector3Int(x, y, 0);
                bool wall = x >= b.xMin - t && x < b.xMax + t && y >= b.yMin - t && y < b.yMax + t;
                if (!wall)
                    outsideCells.Add(cell);
                else if (y < b.yMin)
                    sides[0].Add(cell);
                else if (y >= b.yMax)
                    sides[1].Add(cell);
                else if (x < b.xMin)
                    sides[2].Add(cell);
                else
                    sides[3].Add(cell);
            }
        }

        var rng = new System.Random(seed);
        int order = map.GetComponent<TilemapRenderer>().sortingOrder;
        CreateLayer("Outside", outsideCells, outsideTiles, outsideMaterial, floorY, order, rng, map);

        // Each side is a solid block with its own tile top. The top is linked to the block, so the
        // see-through hole (OccluderFader) cuts both together, and only on the side that hides the player
        var names = new[] { "South", "North", "West", "East" };
        var mins = new[]
        {
            new Vector3Int(b.xMin - t, b.yMin - t), new Vector3Int(b.xMin - t, b.yMax),
            new Vector3Int(b.xMin - t, b.yMin), new Vector3Int(b.xMax, b.yMin),
        };
        var maxs = new[]
        {
            new Vector3Int(b.xMax + t, b.yMin), new Vector3Int(b.xMax + t, b.yMax + t),
            new Vector3Int(b.xMin, b.yMax), new Vector3Int(b.xMax + t, b.yMax),
        };
        for (int i = 0; i < 4; i++)
        {
            var block = CreateBlock("Wall " + names[i], mins[i], maxs[i], floorY);
            var top = CreateLayer("Wall Top " + names[i], sides[i], wallTiles, wallTopMaterial, floorY + wallHeight + 0.01f, order + 1, rng, map);
            if (top != null)
                block.AddComponent<OccluderLink>().alsoCut = new Renderer[] { top };
        }
    }

    TilemapRenderer CreateLayer(string name, List<Vector3Int> cells, TileBase[] tiles, Material material, float worldY, int order, System.Random rng, Tilemap source)
    {
        var choices = tiles?.Where(tile => tile != null).ToArray();
        if (cells.Count == 0 || choices == null || choices.Length == 0)
            return null;

        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(root.transform, false);
        Vector3 p = go.transform.position;
        go.transform.position = new Vector3(p.x, worldY, p.z);

        var map = go.AddComponent<Tilemap>();

        // Match the floor's tile orientation (XZ), or the tiles stand up vertically
        map.orientation = source.orientation;
        map.orientationMatrix = source.orientationMatrix;
        map.tileAnchor = source.tileAnchor;

        var tilemapRenderer = go.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = order;
        if (material != null)
            tilemapRenderer.sharedMaterial = material;

        var picked = new TileBase[cells.Count];
        for (int i = 0; i < picked.Length; i++)
            picked[i] = choices[rng.Next(choices.Length)];
        map.SetTiles(cells.ToArray(), picked);
        return tilemapRenderer;
    }

    // A solid cube covering cells from minCell up to (not including) maxCell, with a collider
    GameObject CreateBlock(string name, Vector3Int minCell, Vector3Int maxCell, float baseY)
    {
        Vector3 a = sourceGrid.CellToWorld(minCell);
        Vector3 c = sourceGrid.CellToWorld(maxCell);

        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.hideFlags = HideFlags.DontSave;
        block.transform.SetParent(root.transform, true);
        block.transform.position = new Vector3((a.x + c.x) / 2f, baseY + wallHeight / 2f, (a.z + c.z) / 2f);
        block.transform.localScale = new Vector3(Mathf.Abs(c.x - a.x), wallHeight, Mathf.Abs(c.z - a.z));

        if (wallSideMaterial != null)
            block.GetComponent<MeshRenderer>().sharedMaterial = wallSideMaterial;
        return block;
    }

    Tilemap FindFloor()
    {
        if (floor != null)
            return floor;

        // First tilemap in our scene that isn't one we generated
        return FindObjectsByType<Tilemap>(FindObjectsSortMode.None)
            .FirstOrDefault(map => map.gameObject.scene == gameObject.scene
                                   && (map.gameObject.hideFlags & HideFlags.DontSave) == 0);
    }

    void Clear()
    {
        if (root != null)
            DestroyObject(root);
        root = null;

        // After a script recompile the reference is lost, so also sweep up by name
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;
        foreach (var go in gameObject.scene.GetRootGameObjects())
            if (go.name == RootName)
                DestroyObject(go);
    }

    static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }
}
