using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Navigation
{
    internal sealed class GridPathfinder
    {
        private const int StraightMoveCost = 10;
        private const int DiagonalMoveCost = 14;

        private static readonly Vector3Int[] NeighborOffsets =
        {
            new(0, 1, 0),
            new(1, 0, 0),
            new(0, -1, 0),
            new(-1, 0, 0),
            new(1, 1, 0),
            new(1, -1, 0),
            new(-1, -1, 0),
            new(-1, 1, 0)
        };

        private readonly Dictionary<Vector3Int, NodeRecord> _nodeRecords = new();
        private readonly HashSet<Vector3Int> _closedCells = new();
        private readonly List<Vector3Int> _openCells = new();

        public bool TryFindPath(
            NavigationMap map,
            Vector3Int startCell,
            Vector3Int destinationCell,
            float clearanceRadius,
            List<Vector3Int> cellPath)
        {
            cellPath.Clear();
            _nodeRecords.Clear();
            _closedCells.Clear();
            _openCells.Clear();

            _nodeRecords[startCell] = new NodeRecord(
                startCell,
                0,
                GetEstimatedCost(startCell, destinationCell));
            _openCells.Add(startCell);

            while (_openCells.Count > 0)
            {
                Vector3Int currentCell = RemoveLowestCostOpenCell();
                if (currentCell == destinationCell)
                {
                    ReconstructPath(startCell, destinationCell, cellPath);
                    return true;
                }

                _closedCells.Add(currentCell);
                NodeRecord currentRecord = _nodeRecords[currentCell];

                for (int index = 0; index < NeighborOffsets.Length; index++)
                {
                    Vector3Int offset = NeighborOffsets[index];
                    Vector3Int neighborCell = currentCell + offset;
                    if (_closedCells.Contains(neighborCell)
                        || !map.CanTraverse(currentCell, neighborCell, clearanceRadius))
                    {
                        continue;
                    }

                    int moveCost = IsDiagonal(offset)
                        ? DiagonalMoveCost
                        : StraightMoveCost;
                    int costFromStart = currentRecord.CostFromStart + moveCost;
                    if (_nodeRecords.TryGetValue(neighborCell, out NodeRecord neighborRecord)
                        && costFromStart >= neighborRecord.CostFromStart)
                    {
                        continue;
                    }

                    _nodeRecords[neighborCell] = new NodeRecord(
                        currentCell,
                        costFromStart,
                        costFromStart + GetEstimatedCost(neighborCell, destinationCell));

                    if (!_openCells.Contains(neighborCell))
                        _openCells.Add(neighborCell);
                }
            }

            return false;
        }

        private Vector3Int RemoveLowestCostOpenCell()
        {
            int bestIndex = 0;
            NodeRecord bestRecord = _nodeRecords[_openCells[0]];

            for (int index = 1; index < _openCells.Count; index++)
            {
                NodeRecord candidateRecord = _nodeRecords[_openCells[index]];
                if (candidateRecord.EstimatedTotalCost >= bestRecord.EstimatedTotalCost)
                    continue;

                bestIndex = index;
                bestRecord = candidateRecord;
            }

            Vector3Int cell = _openCells[bestIndex];
            int lastIndex = _openCells.Count - 1;
            _openCells[bestIndex] = _openCells[lastIndex];
            _openCells.RemoveAt(lastIndex);
            return cell;
        }

        private void ReconstructPath(
            Vector3Int startCell,
            Vector3Int destinationCell,
            List<Vector3Int> cellPath)
        {
            Vector3Int currentCell = destinationCell;
            cellPath.Add(currentCell);

            while (currentCell != startCell)
            {
                currentCell = _nodeRecords[currentCell].ParentCell;
                cellPath.Add(currentCell);
            }

            cellPath.Reverse();
        }

        private static bool IsDiagonal(Vector3Int offset)
        {
            return offset.x != 0 && offset.y != 0;
        }

        private static int GetEstimatedCost(Vector3Int from, Vector3Int to)
        {
            int horizontalDistance = Mathf.Abs(to.x - from.x);
            int verticalDistance = Mathf.Abs(to.y - from.y);
            int diagonalSteps = Mathf.Min(horizontalDistance, verticalDistance);
            int straightSteps = Mathf.Max(horizontalDistance, verticalDistance) - diagonalSteps;
            return diagonalSteps * DiagonalMoveCost + straightSteps * StraightMoveCost;
        }

        private readonly struct NodeRecord
        {
            public Vector3Int ParentCell { get; }

            public int CostFromStart { get; }

            public int EstimatedTotalCost { get; }

            public NodeRecord(
                Vector3Int parentCell,
                int costFromStart,
                int estimatedTotalCost)
            {
                ParentCell = parentCell;
                CostFromStart = costFromStart;
                EstimatedTotalCost = estimatedTotalCost;
            }
        }
    }
}
