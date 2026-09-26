using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameLogic.Navigation
{
    internal sealed class NavigationMap
    {
        private const float ClearanceMargin = 0.02f;
        private const float ClearanceSampleSpacing = 0.05f;

        private readonly Tilemap _groundTilemap;
#if DEBUG
        private readonly Tilemap _collisionTilemap;
#endif
        private readonly Collider2D _collisionCollider;
        private readonly HashSet<Vector3Int> _walkableCells = new();

        public bool HasWalkableCells => _walkableCells.Count > 0;

#if DEBUG
        internal Tilemap GroundTilemap => _groundTilemap;

        internal Tilemap CollisionTilemap => _collisionTilemap;

        internal bool IsBaseWalkable(Vector3Int cell)
        {
            cell.z = 0;
            return _walkableCells.Contains(cell);
        }
#endif

        public NavigationMap(
            Tilemap groundTilemap,
            Tilemap collisionTilemap,
            Collider2D collisionCollider)
        {
            _groundTilemap = groundTilemap;
#if DEBUG
            _collisionTilemap = collisionTilemap;
#endif
            _collisionCollider = collisionCollider;

            BoundsInt bounds = groundTilemap.cellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0)
                return;

            // 两层使用相同范围，批量数组与格子遍历按同一索引对应。
            TileBase[] groundTiles = groundTilemap.GetTilesBlock(bounds);
            TileBase[] collisionTiles = collisionTilemap.GetTilesBlock(bounds);
            int index = 0;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (groundTiles[index] != null && collisionTiles[index] == null)
                    _walkableCells.Add(new Vector3Int(cell.x, cell.y, 0));
                index++;
            }
        }

        public Vector3Int WorldToCell(Vector2 worldPosition)
        {
            Vector3Int cell = _groundTilemap.WorldToCell(worldPosition);
            cell.z = 0;
            return cell;
        }

        public Vector2 GetCellCenterWorld(Vector3Int cell)
        {
            return _groundTilemap.GetCellCenterWorld(cell);
        }

        public bool IsWalkable(Vector3Int cell, float clearanceRadius)
        {
            cell.z = 0;
            if (!_walkableCells.Contains(cell))
                return false;

            return HasClearance(GetCellCenterWorld(cell), clearanceRadius);
        }

        /// <summary>解析端点所在格，要求格中心和实际端点均满足单位净空。</summary>
        public bool TryGetWalkableCell(
            Vector2 position,
            float clearanceRadius,
            out Vector3Int cell)
        {
            cell = WorldToCell(position);
            return IsWalkable(cell, clearanceRadius) && HasClearance(position, clearanceRadius);
        }

        public bool CanTraverse(
            Vector3Int fromCell,
            Vector3Int toCell,
            float clearanceRadius)
        {
            if (!IsWalkable(toCell, clearanceRadius))
                return false;

            Vector3Int offset = toCell - fromCell;
            if (IsDiagonal(offset))
            {
                Vector3Int horizontalCell = fromCell + new Vector3Int(offset.x, 0, 0);
                Vector3Int verticalCell = fromCell + new Vector3Int(0, offset.y, 0);
                if (!IsWalkable(horizontalCell, clearanceRadius)
                    || !IsWalkable(verticalCell, clearanceRadius))
                {
                    return false;
                }
            }

            return HasSegmentClearance(
                GetCellCenterWorld(fromCell),
                GetCellCenterWorld(toCell),
                clearanceRadius);
        }

        public bool HasSegmentClearance(
            Vector2 from,
            Vector2 to,
            float clearanceRadius)
        {
            if (clearanceRadius <= 0f)
                return true;

            float distance = Vector2.Distance(from, to);
            int sampleCount = Mathf.Max(
                1,
                Mathf.CeilToInt(distance / ClearanceSampleSpacing));
            for (int index = 0; index <= sampleCount; index++)
            {
                Vector2 position = Vector2.Lerp(from, to, index / (float)sampleCount);
                if (!HasClearance(position, clearanceRadius))
                    return false;
            }

            return true;
        }

        private bool HasClearance(Vector2 position, float clearanceRadius)
        {
            if (clearanceRadius <= 0f)
                return true;

            Vector2 closestPoint = _collisionCollider.ClosestPoint(position);
            float requiredClearance = clearanceRadius + ClearanceMargin;
            return (closestPoint - position).sqrMagnitude
                   >= requiredClearance * requiredClearance;
        }

        private static bool IsDiagonal(Vector3Int offset)
        {
            return offset.x != 0 && offset.y != 0;
        }
    }
}
