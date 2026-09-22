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
        private readonly Collider2D _collisionCollider;
        private readonly HashSet<Vector3Int> _walkableCells = new();

        public bool HasWalkableCells => _walkableCells.Count > 0;

        public NavigationMap(
            Tilemap groundTilemap,
            Tilemap collisionTilemap,
            Collider2D collisionCollider)
        {
            _groundTilemap = groundTilemap;
            _collisionCollider = collisionCollider;

            foreach (Vector3Int cell in groundTilemap.cellBounds.allPositionsWithin)
            {
                if (groundTilemap.HasTile(cell) && !collisionTilemap.HasTile(cell))
                    _walkableCells.Add(new Vector3Int(cell.x, cell.y, 0));
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

        public bool IsPositionWalkable(Vector2 position, float clearanceRadius)
        {
            Vector3Int cell = WorldToCell(position);
            return _walkableCells.Contains(cell) && HasClearance(position, clearanceRadius);
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
