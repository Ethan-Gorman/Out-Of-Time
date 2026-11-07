using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// A tile that shows a random sprite from a weighted list, picked per cell.
// Create one with Assets > Create > Out Of Time > Weighted Random Tile, add sprites and weights,
// drag it into a Tile Palette, and paint it as a single tile (Default Brush).
// The pick depends only on the cell position, so the same cell always shows the same sprite.
[CreateAssetMenu(menuName = "Out Of Time/Weighted Random Tile", fileName = "New Random Tile")]
public class WeightedRandomTile : TileBase
{
    [Serializable]
    public struct Entry
    {
        public Sprite sprite;

        [Tooltip("Relative chance. 3 is three times as likely as 1")]
        [Min(0)] public int weight;
    }

    public Entry[] sprites = Array.Empty<Entry>();

    [Tooltip("Change for a different random pattern")]
    public int seed;

    public Tile.ColliderType colliderType = Tile.ColliderType.None;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = Pick(position);
        tileData.color = Color.white;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tileData.colliderType = colliderType;
    }

    Sprite Pick(Vector3Int position)
    {
        int total = 0;
        foreach (var entry in sprites)
            if (entry.sprite != null)
                total += entry.weight;
        if (total <= 0)
            return null;

        // Stable "random" number from the cell position, so repainting never reshuffles it
        uint hash = (uint)(position.x * 73856093) ^ (uint)(position.y * 19349663) ^ (uint)(seed * 83492791);
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;

        int roll = (int)(hash % (uint)total);
        foreach (var entry in sprites)
        {
            if (entry.sprite == null)
                continue;
            roll -= entry.weight;
            if (roll < 0)
                return entry.sprite;
        }
        return null;
    }
}
