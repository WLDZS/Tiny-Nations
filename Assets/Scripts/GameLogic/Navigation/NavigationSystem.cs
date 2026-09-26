using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace GameLogic.Navigation
{
    public sealed class NavigationSystem : INavigationSystem
    {
        private const string WorldName = "World";
        private const string GroundPath = "Grid/Ground";
        private const string CollisionPath = "Grid/Collision";

        public NavigationMap Map { get; private set; }

        private readonly GridPathfinder _pathfinder;

        private NavigationSystem(NavigationMap map)
        {
            Map = map;
            _pathfinder = new GridPathfinder(map);
        }

        public bool TryFindPathToRange(
            Vector2 start,
            Vector2 target,
            float reach,
            float bodyRadius,
            List<Vector3> path)
        {
            if (Map == null)
            {
                path?.Clear();
                return false;
            }

            return _pathfinder.TryFindPathToRange(start, target, reach, bodyRadius, path);
        }

        public bool TryFindPathToPoint(
            Vector2 start,
            Vector2 destination,
            float bodyRadius,
            List<Vector3> path)
        {
            if (Map == null)
            {
                path?.Clear();
                return false;
            }

            return _pathfinder.TryFindPathToPoint(start, destination, bodyRadius, path);
        }

        /// <summary>只读取指定场景的 World/Grid，不搜索其他已加载场景。</summary>
        public static bool TryCreate(Scene scene, out NavigationSystem system)
        {
            system = null;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root.name != WorldName)
                    continue;

                Transform groundTransform = root.transform.Find(GroundPath);
                Transform collisionTransform = root.transform.Find(CollisionPath);
                Tilemap groundTilemap = groundTransform != null
                    ? groundTransform.GetComponent<Tilemap>()
                    : null;
                Tilemap collisionTilemap = collisionTransform != null
                    ? collisionTransform.GetComponent<Tilemap>()
                    : null;

                if (!NavigationMap.TryCreate(groundTilemap, collisionTilemap, out NavigationMap map))
                    return false;

                system = new NavigationSystem(map);
                return true;
            }

            return false;
        }

        public void Init()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            Map = null;
        }
    }
}
