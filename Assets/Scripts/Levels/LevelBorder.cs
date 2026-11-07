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
    [Tooltip("The painted floor to wrap. Left empty, it uses the first Tilemap in this scene")]
    public Tilemap floor;

    [Header("Wall")]
    [Tooltip("How many tiles thick the wall is")]
    public int wallThickness = 3;
    public float wallHeight = 1f;

    [Tooltip("Tiles for the top of the wall, picked at random and shown in greyscale. Add a tile more than once to make it more common")]
    public TileBase[] wallTiles;
    public Material wallTopMaterial;
    public Material wallSideMaterial;

    [Header("Outside")]
    [Tooltip("How many tiles of floor to fill beyond the wall")]
    public int outsideMargin = 25;

    [Tooltip("Tiles for the floor outside the wall, picked at random, greyscale and dimmed")]
    public TileBase[] outsideTiles;
    public Material outsideMaterial;

    [Tooltip("Change for a different random pattern. The same seed always gives the same layout")]
    public int seed = 1;

    [Header("Floor")]
    [Tooltip("Put on the painted floor when the game starts, so walls and props cast visible shadows onto it " +
             "(Sprite Receive Shadows). Only in Play mode, so the scene file is never changed")]
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

        // Sort every cell around the floor into wall or outside
        int t = wallThickness, m = outsideMargin;
        var wallCells = new List<Vector3Int>();
        var outsideCells = new List<Vector3Int>();
        for (int x = b.xMin - t - m; x < b.xMax + t + m; x++)
        {
            for (int y = b.yMin - t - m; y < b.yMax + t + m; y++)
            {
                bool inside = x >= b.xMin && x < b.xMax && y >= b.yMin && y < b.yMax;
                if (inside)
                    continue;

                bool wall = x >= b.xMin - t && x < b.xMax + t && y >= b.yMin - t && y < b.yMax + t;
                (wall ? wallCells : outsideCells).Add(new Vector3Int(x, y, 0));
            }
        }

        var rng = new System.Random(seed);
        int order = map.GetComponent<TilemapRenderer>().sortingOrder;
        CreateLayer("Outside", outsideCells, outsideTiles, outsideMaterial, floorY, order, rng, map);
        CreateLayer("Wall Top", wallCells, wallTiles, wallTopMaterial, floorY + wallHeight + 0.01f, order + 1, rng, map);

        // Four solid blocks under the wall tops: south, north, west, east
        CreateBlock("Wall South", new Vector3Int(b.xMin - t, b.yMin - t), new Vector3Int(b.xMax + t, b.yMin), floorY);
        CreateBlock("Wall North", new Vector3Int(b.xMin - t, b.yMax), new Vector3Int(b.xMax + t, b.yMax + t), floorY);
        CreateBlock("Wall West", new Vector3Int(b.xMin - t, b.yMin), new Vector3Int(b.xMin, b.yMax), floorY);
        CreateBlock("Wall East", new Vector3Int(b.xMax, b.yMin), new Vector3Int(b.xMax + t, b.yMax), floorY);
    }

    void CreateLayer(string name, List<Vector3Int> cells, TileBase[] tiles, Material material, float worldY, int order, System.Random rng, Tilemap source)
    {
        var choices = tiles?.Where(tile => tile != null).ToArray();
        if (cells.Count == 0 || choices == null || choices.Length == 0)
            return;

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
    }

    // A solid cube covering cells from minCell up to (not including) maxCell, with a collider
    void CreateBlock(string name, Vector3Int minCell, Vector3Int maxCell, float baseY)
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
