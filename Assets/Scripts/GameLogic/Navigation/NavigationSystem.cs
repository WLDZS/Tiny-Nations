using System.Collections.Generic;
using BorFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace GameLogic.Navigation
{
    public sealed class NavigationSystem : INavigationSystem
    {
        private const string GroundPath = "World/Grid/Ground";
        private const string CollisionPath = "World/Grid/Collision";

        private readonly GridPathfinder _pathfinder = new();
        private readonly List<Vector3Int> _cellPath = new();
        private NavigationMap _map;
        private Scene _mapScene;
        private bool _started;

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
            TryBindScene(activeScene, true);
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
            Vector3Int startCell = _map.WorldToCell(startAnchorPosition);
            Vector3Int destinationCell = _map.WorldToCell(destinationAnchorPosition);

            if (!_map.IsWalkable(startCell, clearanceRadius)
                || !_map.IsPositionWalkable(startAnchorPosition, clearanceRadius))
            {
                return EPathQueryStatus.InvalidStart;
            }

            if (!_map.IsWalkable(destinationCell, clearanceRadius)
                || !_map.IsPositionWalkable(destinationAnchorPosition, clearanceRadius))
            {
                return EPathQueryStatus.InvalidDestination;
            }

            if (startCell == destinationCell)
            {
                return BuildSameCellPath(
                    startCell,
                    startWorldPosition,
                    destinationWorldPosition,
                    navigationAnchorOffset,
                    clearanceRadius,
                    waypoints);
            }

            if (!_pathfinder.TryFindPath(
                    _map,
                    startCell,
                    destinationCell,
                    clearanceRadius,
                    _cellPath))
            {
                return EPathQueryStatus.NoPath;
            }

            if (!TryBuildWaypoints(
                    destinationWorldPosition,
                    navigationAnchorOffset,
                    clearanceRadius,
                    waypoints))
            {
                waypoints.Clear();
                return EPathQueryStatus.InvalidDestination;
            }

            if (!EnsureStartConnection(
                    startCell,
                    startWorldPosition,
                    navigationAnchorOffset,
                    clearanceRadius,
                    waypoints))
            {
                waypoints.Clear();
                return EPathQueryStatus.InvalidStart;
            }

            return EPathQueryStatus.Success;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool reportMissingMap = mode == LoadSceneMode.Single;
            if (TryBindScene(scene, reportMissingMap))
                return;

            if (mode == LoadSceneMode.Single)
                ClearMap();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == _mapScene)
                ClearMap();
        }

        private bool TryBindScene(Scene scene, bool reportMissingMap)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            Transform groundTransform = FindSceneTransform(scene, GroundPath);
            Transform collisionTransform = FindSceneTransform(scene, CollisionPath);
            if (groundTransform == null || collisionTransform == null)
            {
                if (reportMissingMap)
                {
                    Debug.LogError(
                        $"导航地图绑定失败：场景 {scene.name} 缺少 {GroundPath} 或 {CollisionPath}。");
                }

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
            Vector2 startWorldPosition,
            Vector2 destinationWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            List<Vector2> waypoints)
        {
            Vector2 startAnchorPosition = startWorldPosition + navigationAnchorOffset;
            Vector2 destinationAnchorPosition = destinationWorldPosition + navigationAnchorOffset;
            if (_map.HasSegmentClearance(
                    startAnchorPosition,
                    destinationAnchorPosition,
                    clearanceRadius))
            {
                waypoints.Add(destinationWorldPosition);
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

            Vector2 cellCenterWaypoint = cellCenter - navigationAnchorOffset;
            if ((cellCenterWaypoint - startWorldPosition).sqrMagnitude > Mathf.Epsilon)
                waypoints.Add(cellCenterWaypoint);

            waypoints.Add(destinationWorldPosition);
            return EPathQueryStatus.Success;
        }

        private bool TryBuildWaypoints(
            Vector2 destinationWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            ICollection<Vector2> waypoints)
        {
            for (int index = 1; index < _cellPath.Count - 1; index++)
            {
                Vector3Int previousDirection = _cellPath[index] - _cellPath[index - 1];
                Vector3Int nextDirection = _cellPath[index + 1] - _cellPath[index];
                if (previousDirection == nextDirection)
                    continue;

                Vector2 worldPosition = _map.GetCellCenterWorld(_cellPath[index]);
                waypoints.Add(worldPosition - navigationAnchorOffset);
            }

            Vector3Int destinationCell = _cellPath[_cellPath.Count - 1];
            Vector2 destinationCellCenter = _map.GetCellCenterWorld(destinationCell);
            Vector2 destinationAnchorPosition = destinationWorldPosition + navigationAnchorOffset;
            if ((destinationAnchorPosition - destinationCellCenter).sqrMagnitude > Mathf.Epsilon)
            {
                if (!_map.HasSegmentClearance(
                        destinationCellCenter,
                        destinationAnchorPosition,
                        clearanceRadius))
                {
                    return false;
                }

                waypoints.Add(destinationCellCenter - navigationAnchorOffset);
            }

            waypoints.Add(destinationWorldPosition);
            return true;
        }

        private bool EnsureStartConnection(
            Vector3Int startCell,
            Vector2 startWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            List<Vector2> waypoints)
        {
            if (waypoints.Count == 0)
                return false;

            Vector2 startAnchorPosition = startWorldPosition + navigationAnchorOffset;
            Vector2 firstWaypointAnchorPosition = waypoints[0] + navigationAnchorOffset;
            if (_map.HasSegmentClearance(
                    startAnchorPosition,
                    firstWaypointAnchorPosition,
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

            Vector2 startCellWaypoint = startCellCenter - navigationAnchorOffset;
            if ((startCellWaypoint - waypoints[0]).sqrMagnitude > Mathf.Epsilon)
                waypoints.Insert(0, startCellWaypoint);

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
        }
    }
}
