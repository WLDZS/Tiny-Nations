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
