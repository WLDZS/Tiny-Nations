using System.Collections.Generic;
using BorFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace GameLogic.Navigation
{
    public sealed class NavigationSystem : INavigationSystem
    {
        private const string GridPath = "World/Grid";
        private const int ApproachSearchRadiusCells = 2;
        private const int ApproachRefinementSteps = 8;

        private readonly GridPathfinder _pathfinder = new();
        private readonly List<Vector3Int> _cellPath = new();
        private readonly List<Vector3Int> _approachCells = new();
        private NavigationMap _map;
        private Scene _mapScene;
        private bool _started;

#if DEBUG
        internal NavigationMap DebugMap => _map;
#endif

        public void Init()
        {
        }

        public void Start()
        {
            if (_started)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _started = true;

            Scene activeScene = SceneManager.GetActiveScene();
            ClearMap();
            TryBindScene(activeScene);
        }

        public void Stop()
        {
            if (!_started)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _started = false;
            ClearMap();
        }

        public void Dispose()
        {
            Stop();
        }

        public EPathQueryStatus FindPath(
            Vector2 startWorldPosition,
            Vector2 destinationWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            List<Vector2> waypoints)
        {
            waypoints?.Clear();
            if (waypoints == null)
                return EPathQueryStatus.InvalidRequest;

            if (_map == null)
                return EPathQueryStatus.MapUnavailable;

            clearanceRadius = Mathf.Max(0f, clearanceRadius);
            Vector2 startAnchorPosition = startWorldPosition + navigationAnchorOffset;
            Vector2 destinationAnchorPosition = destinationWorldPosition + navigationAnchorOffset;
            if (!_map.TryGetWalkableCell(startAnchorPosition, clearanceRadius, out Vector3Int startCell))
                return EPathQueryStatus.InvalidStart;

            if (!_map.TryGetWalkableCell(destinationAnchorPosition, clearanceRadius, out Vector3Int destinationCell))
                return EPathQueryStatus.InvalidDestination;

            EPathQueryStatus status;
            if (startCell == destinationCell)
            {
                status = BuildSameCellPath(
                    startCell,
                    startAnchorPosition,
                    destinationAnchorPosition,
                    clearanceRadius,
                    waypoints);
            }
            else if (!_pathfinder.TryFindPath(
                    _map,
                    startCell,
                    destinationCell,
                    clearanceRadius,
                    _cellPath))
            {
                status = EPathQueryStatus.NoPath;
            }
            else if (!TryBuildWaypoints(
                    destinationAnchorPosition,
                    clearanceRadius,
                    waypoints))
            {
                status = EPathQueryStatus.InvalidDestination;
            }
            else if (!EnsureStartConnection(
                    startCell,
                    startAnchorPosition,
                    clearanceRadius,
                    waypoints))
            {
                status = EPathQueryStatus.InvalidStart;
            }
            else
            {
                status = EPathQueryStatus.Success;
            }

            if (status != EPathQueryStatus.Success)
            {
                waypoints.Clear();
                return status;
            }

            // 内部只处理导航中心，成功后统一还原单位 WordPos 坐标。
            for (int index = 0; index < waypoints.Count - 1; index++)
                waypoints[index] -= navigationAnchorOffset;

            waypoints[waypoints.Count - 1] = destinationWorldPosition;
            return EPathQueryStatus.Success;
        }

        public EPathQueryStatus FindApproachPath(
            Vector2 startWorldPosition,
            Vector2 destinationWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            List<Vector2> waypoints,
            out Vector2 reachedDestination)
        {
            reachedDestination = default;
            EPathQueryStatus status = FindPath(
                startWorldPosition,
                destinationWorldPosition,
                navigationAnchorOffset,
                clearanceRadius,
                waypoints);
            if (status == EPathQueryStatus.Success)
            {
                reachedDestination = destinationWorldPosition;
                return status;
            }

            if (status != EPathQueryStatus.InvalidDestination
                && status != EPathQueryStatus.NoPath)
            {
                return status;
            }

            Vector2 destinationAnchorPosition = destinationWorldPosition + navigationAnchorOffset;
            Vector3Int destinationCell = _map.WorldToCell(destinationAnchorPosition);
            _approachCells.Clear();
            for (int y = -ApproachSearchRadiusCells; y <= ApproachSearchRadiusCells; y++)
            {
                for (int x = -ApproachSearchRadiusCells; x <= ApproachSearchRadiusCells; x++)
                {
                    Vector3Int cell = destinationCell + new Vector3Int(x, y, 0);
                    if (_map.IsWalkable(cell, clearanceRadius))
                        _approachCells.Add(cell);
                }
            }

            while (_approachCells.Count > 0)
            {
                int closestIndex = FindClosestApproachCellIndex(destinationAnchorPosition);
                Vector3Int cell = _approachCells[closestIndex];
                _approachCells.RemoveAt(closestIndex);

                Vector2 cellCenter = _map.GetCellCenterWorld(cell);
                Vector2 candidateDestination = cellCenter - navigationAnchorOffset;
                if (FindPath(
                        startWorldPosition,
                        candidateDestination,
                        navigationAnchorOffset,
                        clearanceRadius,
                        waypoints) != EPathQueryStatus.Success)
                {
                    continue;
                }

                Vector2 approachPosition = FindClosestSafeApproachPosition(
                    cell,
                    cellCenter,
                    destinationAnchorPosition,
                    clearanceRadius);
                reachedDestination = approachPosition - navigationAnchorOffset;
                if ((reachedDestination - candidateDestination).sqrMagnitude > Mathf.Epsilon)
                    waypoints.Add(reachedDestination);

                _approachCells.Clear();
                return EPathQueryStatus.Approach;
            }

            waypoints.Clear();
            return status;
        }

        private int FindClosestApproachCellIndex(Vector2 destinationAnchorPosition)
        {
            int bestIndex = 0;
            float bestDistanceSquared = float.PositiveInfinity;
            for (int index = 0; index < _approachCells.Count; index++)
            {
                Vector2 center = _map.GetCellCenterWorld(_approachCells[index]);
                float distanceSquared = (center - destinationAnchorPosition).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestIndex = index;
                bestDistanceSquared = distanceSquared;
            }

            return bestIndex;
        }

        private Vector2 FindClosestSafeApproachPosition(
            Vector3Int cell,
            Vector2 cellCenter,
            Vector2 destinationAnchorPosition,
            float clearanceRadius)
        {
            float safeFraction = 0f;
            float unsafeFraction = 1f;
            for (int index = 0; index < ApproachRefinementSteps; index++)
            {
                float candidateFraction = (safeFraction + unsafeFraction) * 0.5f;
                Vector2 candidate = Vector2.Lerp(
                    cellCenter,
                    destinationAnchorPosition,
                    candidateFraction);
                if (_map.TryGetWalkableCell(candidate, clearanceRadius, out Vector3Int candidateCell)
                    && candidateCell == cell
                    && _map.HasSegmentClearance(cellCenter, candidate, clearanceRadius))
                {
                    safeFraction = candidateFraction;
                }
                else
                {
                    unsafeFraction = candidateFraction;
                }
            }

            return Vector2.Lerp(cellCenter, destinationAnchorPosition, safeFraction);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (TryBindScene(scene))
                return;

            if (mode == LoadSceneMode.Single)
                ClearMap();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == _mapScene)
                ClearMap();
        }

        private bool TryBindScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            Transform gridTransform = FindSceneTransform(scene, GridPath);
            if (gridTransform == null)
                return false;

            Transform groundTransform = gridTransform.Find("Ground");
            Transform collisionTransform = gridTransform.Find("Collision");
            // 菜单等场景不声明导航地图；只诊断已声明但不完整的地图。
            if (groundTransform == null && collisionTransform == null)
                return false;

            if (groundTransform == null || collisionTransform == null)
            {
                Debug.LogError(
                    $"导航地图绑定失败：场景 {scene.name} 缺少 {GridPath}/Ground 或 {GridPath}/Collision。");
                return false;
            }

            Tilemap groundTilemap = groundTransform.GetComponent<Tilemap>();
            Tilemap collisionTilemap = collisionTransform.GetComponent<Tilemap>();
            if (groundTilemap == null || collisionTilemap == null)
            {
                Debug.LogError(
                    $"导航地图绑定失败：场景 {scene.name} 的 Ground 或 Collision 缺少 Tilemap。",
                    groundTransform);
                return false;
            }

            Collider2D collisionCollider = collisionTransform.GetComponent<CompositeCollider2D>();
            if (collisionCollider == null)
                collisionCollider = collisionTransform.GetComponent<Collider2D>();

            if (collisionCollider == null)
            {
                Debug.LogError(
                    $"导航地图绑定失败：场景 {scene.name} 的 Collision 缺少 Collider2D。",
                    collisionTransform);
                return false;
            }

            var map = new NavigationMap(
                groundTilemap,
                collisionTilemap,
                collisionCollider);
            if (!map.HasWalkableCells)
            {
                Debug.LogError(
                    $"导航地图绑定失败：场景 {scene.name} 没有可行走的 Ground 格子。",
                    groundTransform);
                return false;
            }

            _map = map;
            _mapScene = scene;
            _cellPath.Clear();
            return true;
        }

        private EPathQueryStatus BuildSameCellPath(
            Vector3Int startCell,
            Vector2 startAnchorPosition,
            Vector2 destinationAnchorPosition,
            float clearanceRadius,
            List<Vector2> waypoints)
        {
            if (_map.HasSegmentClearance(
                    startAnchorPosition,
                    destinationAnchorPosition,
                    clearanceRadius))
            {
                waypoints.Add(destinationAnchorPosition);
                return EPathQueryStatus.Success;
            }

            Vector2 cellCenter = _map.GetCellCenterWorld(startCell);
            if (!_map.HasSegmentClearance(
                    startAnchorPosition,
                    cellCenter,
                    clearanceRadius))
            {
                return EPathQueryStatus.InvalidStart;
            }

            if (!_map.HasSegmentClearance(
                    cellCenter,
                    destinationAnchorPosition,
                    clearanceRadius))
            {
                return EPathQueryStatus.InvalidDestination;
            }

            if ((cellCenter - startAnchorPosition).sqrMagnitude > Mathf.Epsilon)
                waypoints.Add(cellCenter);

            waypoints.Add(destinationAnchorPosition);
            return EPathQueryStatus.Success;
        }

        private bool TryBuildWaypoints(
            Vector2 destinationAnchorPosition,
            float clearanceRadius,
            ICollection<Vector2> waypoints)
        {
            for (int index = 1; index < _cellPath.Count - 1; index++)
            {
                Vector3Int previousDirection = _cellPath[index] - _cellPath[index - 1];
                Vector3Int nextDirection = _cellPath[index + 1] - _cellPath[index];
                if (previousDirection == nextDirection)
                    continue;

                waypoints.Add(_map.GetCellCenterWorld(_cellPath[index]));
            }

            Vector3Int destinationCell = _cellPath[_cellPath.Count - 1];
            Vector2 destinationCellCenter = _map.GetCellCenterWorld(destinationCell);
            if ((destinationAnchorPosition - destinationCellCenter).sqrMagnitude > Mathf.Epsilon)
            {
                if (!_map.HasSegmentClearance(
                        destinationCellCenter,
                        destinationAnchorPosition,
                        clearanceRadius))
                {
                    return false;
                }

                waypoints.Add(destinationCellCenter);
            }

            waypoints.Add(destinationAnchorPosition);
            return true;
        }

        private bool EnsureStartConnection(
            Vector3Int startCell,
            Vector2 startAnchorPosition,
            float clearanceRadius,
            List<Vector2> waypoints)
        {
            if (waypoints.Count == 0)
                return false;

            if (_map.HasSegmentClearance(
                    startAnchorPosition,
                    waypoints[0],
                    clearanceRadius))
            {
                return true;
            }

            Vector2 startCellCenter = _map.GetCellCenterWorld(startCell);
            if (!_map.HasSegmentClearance(
                    startAnchorPosition,
                    startCellCenter,
                    clearanceRadius))
            {
                return false;
            }

            if ((startCellCenter - waypoints[0]).sqrMagnitude > Mathf.Epsilon)
                waypoints.Insert(0, startCellCenter);

            return true;
        }

        private static Transform FindSceneTransform(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = null;

            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name != parts[0])
                    continue;

                current = roots[index].transform;
                break;
            }

            for (int index = 1; current != null && index < parts.Length; index++)
                current = current.Find(parts[index]);

            return current;
        }

        private void ClearMap()
        {
            _map = null;
            _mapScene = default;
            _cellPath.Clear();
            _approachCells.Clear();
        }
    }
}
