using System.Collections.Generic;
using BorFramework;
using UnityEngine;

namespace GameLogic.Navigation
{
    public interface INavigationSystem : IGameSystem
    {
        /// <summary>
        /// Finds a synchronous path between unit WordPos world positions.
        /// The caller owns <paramref name="waypoints"/>.
        /// It is cleared before every request and remains empty on failure.
        /// Requests run synchronously on the main thread and are not reentrant.
        /// </summary>
        /// <param name="startWorldPosition">Unit WordPos start position in world units.</param>
        /// <param name="destinationWorldPosition">Requested unit WordPos destination in world units.</param>
        /// <param name="navigationAnchorOffset">World-space offset from WordPos to the navigation center.</param>
        /// <param name="clearanceRadius">Navigation-center clearance radius in world units.</param>
        /// <param name="waypoints">Caller-owned output list of unit WordPos world positions.</param>
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

        /// <summary>
        /// Finds a path toward a moving target. If its exact position cannot be reached,
        /// the path may end at a nearby reachable position instead.
        /// </summary>
        /// <param name="startWorldPosition">Unit WordPos start position in world units.</param>
        /// <param name="destinationWorldPosition">Target unit WordPos position in world units.</param>
        /// <param name="navigationAnchorOffset">World-space offset from WordPos to the navigation center.</param>
        /// <param name="clearanceRadius">Navigation-center clearance radius in world units.</param>
        /// <param name="waypoints">Caller-owned output list, cleared on failure.</param>
        /// <param name="reachedDestination">Actual unit WordPos endpoint, or zero on failure.</param>
        /// <returns>Success for the exact target, Approach for a nearby reachable point, or a failure result.</returns>
        EPathQueryStatus FindApproachPath(
            Vector2 startWorldPosition,
            Vector2 destinationWorldPosition,
            Vector2 navigationAnchorOffset,
            float clearanceRadius,
            List<Vector2> waypoints,
            out Vector2 reachedDestination);
    }
}
