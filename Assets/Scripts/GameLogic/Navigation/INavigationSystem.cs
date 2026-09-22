using System.Collections.Generic;
using BorFramework;
using UnityEngine;

namespace GameLogic.Navigation
{
    public interface INavigationSystem : IGameSystem
    {
        /// <summary>
        /// Finds a synchronous path between unit-root world positions.
        /// The caller owns <paramref name="waypoints"/>.
        /// It is cleared before every request and remains empty on failure.
        /// Requests run synchronously on the main thread and are not reentrant.
        /// </summary>
        /// <param name="startWorldPosition">Unit-root start position in world units.</param>
        /// <param name="destinationWorldPosition">Requested unit-root destination in world units.</param>
        /// <param name="navigationAnchorOffset">World-space offset from the unit root to its navigation center.</param>
        /// <param name="clearanceRadius">Navigation-center clearance radius in world units.</param>
        /// <param name="waypoints">Caller-owned output list of unit-root world positions.</param>
        /// <returns>
        /// A result describing whether and why the request completed.
        /// Successful paths avoid blocked cells and end at the exact requested destination.
        /// </returns>
        EPathQueryStatus FindPath(
            Vector2 startWorldPosition,
            Vector2 destinationWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            List<Vector2> waypoints);
    }
}
