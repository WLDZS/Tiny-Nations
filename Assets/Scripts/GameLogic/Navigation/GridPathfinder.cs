using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Navigation
{
    internal sealed class GridPathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            new(1, 0), new(0, 1), new(-1, 0), new(0, -1),
            new(1, 1), new(-1, 1), new(-1, -1), new(1, -1)
        };

        private readonly NavigationMap _map;

        public GridPathfinder(NavigationMap map)
        {
            _map = map;
        }

        public bool TryFindPathToRange(
            Vector2 start,
            Vector2 target,
            float reach,
            float bodyRadius,
            List<Vector3> path)
        {
            return TryFindPath(start, target, reach, bodyRadius, path, false);
        }

        public bool TryFindPathToPoint(
            Vector2 start,
            Vector2 destination,
            float bodyRadius,
            List<Vector3> path)
        {
            return TryFindPath(start, destination, 0f, bodyRadius, path, true);
        }

        private bool TryFindPath(
            Vector2 start,
            Vector2 target,
            float reach,
            float bodyRadius,
            List<Vector3> path,
            bool exactDestination)
        {
            if (path == null)
                return false;

            path.Clear();
            if (_map == null || (!exactDestination && reach <= 0f) || bodyRadius < 0f
                || !_map.TryGetCell(start, out NavigationCell startCell)
                || !startCell.IsWalkable
                || !_map.CanStandAt(start, bodyRadius)
                || !_map.CanStandAt(_map.GetCellCenterWorld(startCell.Position), bodyRadius))
            {
                return false;
            }

            NavigationCell goalCell = default;
            if (exactDestination && (!_map.TryGetCell(target, out goalCell)
                || !goalCell.IsWalkable || !_map.CanStandAt(target, bodyRadius)))
            {
                return false;
            }

            if (exactDestination && _map.CanTraverseSegment(start, target, bodyRadius))
            {
                path.Add(target);
                return true;
            }

            Vector3Int goalPosition = goalCell.Position;

            BoundsInt bounds = _map.CellBounds;
            int width = bounds.size.x;
            int count = width * bounds.size.y;
            var costs = new float[count];
            var previous = new int[count];
            var closed = new bool[count];
            var inOpen = new bool[count];
            for (int i = 0; i < count; i++)
            {
                costs[i] = float.PositiveInfinity;
                previous[i] = -1;
            }

            int startIndex = GetIndex(startCell.Position, bounds, width);
            float goalReach = reach - Mathf.Min(0.05f, reach * 0.5f);
            costs[startIndex] = 0f;
            var open = new List<int> { startIndex };
            inOpen[startIndex] = true;

            while (open.Count > 0)
            {
                int bestOpenIndex = 0;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    int index = open[i];
                    Vector3Int position = GetPosition(index, bounds, width);
                    float remaining = Mathf.Max(0f,
                        Vector2.Distance(_map.GetCellCenterWorld(position), target) - reach);
                    float score = costs[index] + remaining;
                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    bestOpenIndex = i;
                }

                int currentIndex = open[bestOpenIndex];
                open.RemoveAt(bestOpenIndex);
                inOpen[currentIndex] = false;
                closed[currentIndex] = true;
                Vector3Int current = GetPosition(currentIndex, bounds, width);
                Vector3 currentCenter = _map.GetCellCenterWorld(current);
                bool reached = exactDestination
                    ? current == goalPosition
                      && _map.CanTraverseSegment(currentCenter, target, bodyRadius)
                    : ((Vector2)currentCenter - target).sqrMagnitude <= goalReach * goalReach;
                if (reached && _map.CanStandAt(currentCenter, bodyRadius))
                {
                    BuildPath(currentIndex, startIndex, previous, bounds, width,
                        start, bodyRadius, path);
                    if (exactDestination)
                        path.Add(target);
                    return true;
                }

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int direction = Directions[i];
                    Vector3Int next = current + new Vector3Int(direction.x, direction.y, 0);
                    if (!CanTraverse(current, next, direction, bodyRadius))
                        continue;

                    int nextIndex = GetIndex(next, bounds, width);
                    if (closed[nextIndex])
                        continue;

                    Vector3 nextCenter = _map.GetCellCenterWorld(next);
                    float cost = costs[currentIndex] + Vector2.Distance(currentCenter, nextCenter);
                    if (cost >= costs[nextIndex])
                        continue;

                    costs[nextIndex] = cost;
                    previous[nextIndex] = currentIndex;
                    if (inOpen[nextIndex])
                        continue;

                    open.Add(nextIndex);
                    inOpen[nextIndex] = true;
                }
            }

            return false;
        }

        private bool CanTraverse(
            Vector3Int current,
            Vector3Int next,
            Vector2Int direction,
            float bodyRadius)
        {
            if (!_map.TryGetCell(next, out NavigationCell nextCell)
                || !nextCell.IsWalkable
                || !_map.CanStandAt(_map.GetCellCenterWorld(next), bodyRadius))
            {
                return false;
            }

            if (direction.x != 0 && direction.y != 0)
            {
                Vector3Int horizontal = current + new Vector3Int(direction.x, 0, 0);
                Vector3Int vertical = current + new Vector3Int(0, direction.y, 0);
                if (!_map.CanStandAt(_map.GetCellCenterWorld(horizontal), bodyRadius)
                    || !_map.CanStandAt(_map.GetCellCenterWorld(vertical), bodyRadius))
                {
                    return false;
                }
            }

            Vector3 midpoint = (_map.GetCellCenterWorld(current) + _map.GetCellCenterWorld(next)) * 0.5f;
            return _map.CanStandAt(midpoint, bodyRadius);
        }

        private void BuildPath(
            int goalIndex,
            int startIndex,
            int[] previous,
            BoundsInt bounds,
            int width,
            Vector2 start,
            float bodyRadius,
            List<Vector3> path)
        {
            var reversed = new List<Vector3>();
            int index = goalIndex;
            while (index != startIndex)
            {
                reversed.Add(_map.GetCellCenterWorld(GetPosition(index, bounds, width)));
                index = previous[index];
            }

            Vector3 startCenter = _map.GetCellCenterWorld(GetPosition(startIndex, bounds, width));
            // 重算移动目标路径时，安全则直接去下一格，避免反复折返当前格心。
            if (reversed.Count == 0
                || !_map.CanTraverseSegment(start, reversed[reversed.Count - 1], bodyRadius))
            {
                path.Add(startCenter);
            }

            for (int i = reversed.Count - 1; i >= 0; i--)
                path.Add(reversed[i]);
        }

        private static int GetIndex(Vector3Int position, BoundsInt bounds, int width)
        {
            return (position.y - bounds.yMin) * width + position.x - bounds.xMin;
        }

        private static Vector3Int GetPosition(int index, BoundsInt bounds, int width)
        {
            return new Vector3Int(
                bounds.xMin + index % width,
                bounds.yMin + index / width,
                bounds.zMin);
        }
    }
}
