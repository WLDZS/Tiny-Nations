using UnityEngine;

namespace GameLogic.Units
{
    public interface IUnitQuery
    {
        bool TryGetUnit(Collider2D collider, out UnitEntity unit);

        /// <summary>
        /// Gets the current scene transform for a spawned unit.
        /// </summary>
        bool TryGetUnitTransform(UnitEntity unit, out Transform transform);

        /// <summary>Gets the WordPos world position, or the unit root position when WordPos is absent.</summary>
        bool TryGetUnitWorldPosition(UnitEntity unit, out Vector3 position);

        /// <summary>Reserves a reachable position around a target for one pursuing unit.</summary>
        bool TryGetApproachPosition(UnitEntity source, UnitEntity target, out Vector3 position);

        /// <summary>Releases a pursuing unit's reserved position when its target is lost.</summary>
        void ReleaseApproachPosition(UnitEntity source);

        /// <summary>
        /// Finds the closest living enemy inside the supplied world-space range.
        /// </summary>
        bool TryFindClosestEnemy(
            UnitEntity source,
            Vector3 origin,
            float maxDistance,
            out UnitEntity unit);
    }
}
