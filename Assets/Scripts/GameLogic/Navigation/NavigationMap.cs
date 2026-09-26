using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameLogic.Navigation
{
    public sealed class NavigationMap
    {
        private readonly Tilemap _groundTilemap;
        private readonly NavigationCell[,] _cells;

        public BoundsInt CellBounds { get; }

        public int WalkableCellCount { get; }

        private NavigationMap(
            Tilemap groundTilemap,
            BoundsInt cellBounds,
            NavigationCell[,] cells,
            int walkableCellCount)
        {
            _groundTilemap = groundTilemap;
            CellBounds = cellBounds;
            _cells = cells;
            WalkableCellCount = walkableCellCount;
        }

        /// <summary>从 Ground 的格子范围建图；Collision 中有 Tile 的格子整体视为禁行。</summary>
        public static bool TryCreate(Tilemap groundTilemap, Tilemap collisionTilemap, out NavigationMap map)
        {
            map = null;
            if (groundTilemap == null || collisionTilemap == null)
                return false;

            if (groundTilemap.transform.parent == null
                || groundTilemap.transform.parent != collisionTilemap.transform.parent)
                return false;

            BoundsInt bounds = groundTilemap.cellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z != 1)
                return false;

            var cells = new NavigationCell[bounds.size.x, bounds.size.y];
            int walkableCellCount = 0;
            foreach (Vector3Int position in bounds.allPositionsWithin)
            {
                bool hasGround = groundTilemap.HasTile(position);
                Vector3 worldCenter = groundTilemap.GetCellCenterWorld(position);
                bool hasCollision = collisionTilemap.HasTile(collisionTilemap.WorldToCell(worldCenter));
                var cell = new NavigationCell(position, hasGround, hasCollision);
                cells[position.x - bounds.xMin, position.y - bounds.yMin] = cell;

                if (cell.IsWalkable)
                    walkableCellCount++;
            }

            if (walkableCellCount == 0)
                return false;

            map = new NavigationMap(groundTilemap, bounds, cells, walkableCellCount);
            return true;
        }

        /// <summary>只返回 Ground 范围内的格子；没有 Ground Tile 的格子仍可返回，但不可行走。</summary>
        public bool TryGetCell(Vector3Int position, out NavigationCell cell)
        {
            cell = default;
            if (!CellBounds.Contains(position))
                return false;

            cell = _cells[position.x - CellBounds.xMin, position.y - CellBounds.yMin];
            return true;
        }

        public bool TryGetCell(Vector3 worldPosition, out NavigationCell cell)
        {
            return TryGetCell(_groundTilemap.WorldToCell(worldPosition), out cell);
        }

        public Vector3 GetCellCenterWorld(Vector3Int position)
        {
            return _groundTilemap.GetCellCenterWorld(position);
        }

        /// <summary>检查身体圆形占地是否完全落在可行走的 Ground 上。</summary>
        public bool CanStandAt(Vector2 center, float radius)
        {
            if (radius < 0f || !TryGetCell(center, out NavigationCell current) || !current.IsWalkable)
                return false;

            if (radius <= 0f)
                return true;

            Vector3Int first = _groundTilemap.WorldToCell(center - Vector2.one * radius);
            Vector3Int last = _groundTilemap.WorldToCell(center + Vector2.one * radius);
            for (int x = first.x; x <= last.x; x++)
            {
                for (int y = first.y; y <= last.y; y++)
                {
                    var position = new Vector3Int(x, y, current.Position.z);
                    if (TryGetCell(position, out NavigationCell cell) && cell.IsWalkable)
                        continue;

                    GetCellWorldCorners(position, out Vector3 a, out Vector3 b);
                    float nearestX = Mathf.Clamp(center.x, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x));
                    float nearestY = Mathf.Clamp(center.y, Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y));
                    if ((new Vector2(nearestX, nearestY) - center).sqrMagnitude < radius * radius)
                        return false;
                }
            }

            return true;
        }

        internal bool CanTraverseSegment(Vector2 start, Vector2 end, float radius)
        {
            float distance = Vector2.Distance(start, end);
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
            for (int i = 0; i <= sampleCount; i++)
            {
                if (!CanStandAt(Vector2.Lerp(start, end, i / (float)sampleCount), radius))
                    return false;
            }

            return true;
        }

        internal void GetCellWorldCorners(Vector3Int position, out Vector3 first, out Vector3 opposite)
        {
            first = _groundTilemap.CellToWorld(position);
            opposite = _groundTilemap.CellToWorld(position + new Vector3Int(1, 1, 0));
        }
    }
}
