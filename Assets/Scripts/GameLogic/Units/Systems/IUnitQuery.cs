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

        /// <summary>Gets the center and radius used for attack range checks.</summary>
        bool TryGetUnitAttackFootprint(UnitEntity unit, out Vector2 center, out float radius);

        bool IsTargetInAttackRange(UnitEntity source, UnitEntity target, float attackRange);

        bool TryGetApproachPosition(
            UnitEntity source,
            UnitEntity target,
            float attackRange,
            out Vector2 position,
            out bool canAttack);

        void ReleaseApproachPosition(UnitEntity source);

        Vector2 GetLocalSeparation(UnitEntity source);

        bool TryFindClosestEnemyInAttackRange(
            UnitEntity source,
            float attackRange,
            out UnitEntity unit);

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
