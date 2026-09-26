using System.Collections.Generic;
using BorFramework;
using UnityEngine;

namespace GameLogic.Navigation
{
    public interface INavigationSystem : IGameSystem
    {
        NavigationMap Map { get; }

        bool TryFindPathToRange(
            Vector2 start,
            Vector2 target,
            float reach,
            float bodyRadius,
            List<Vector3> path);

        bool TryFindPathToPoint(
            Vector2 start,
            Vector2 destination,
            float bodyRadius,
            List<Vector3> path);
    }
}
