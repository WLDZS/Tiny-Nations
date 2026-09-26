using System.Collections.Generic;
using GameLogic.Navigation;
using UnityEngine;

namespace GameLogic.Units
{
    internal sealed class UnitApproachSlots
    {
        private const int Directions = 16;
        private const int WaitingRings = 4;
        private const float MinimumCrowdRadius = 0.22f;
        private const float SlotGap = 0.06f;

        private readonly Dictionary<UnitEntity, (UnitEntity Target, Vector2 Offset, float Radius, bool CanAttack)>
            _reservations = new();
        private readonly List<UnitEntity> _releaseBuffer = new();

        public bool TryGetPosition(
            UnitEntity source,
            UnitEntity target,
            Vector2 sourceCenter,
            float sourceRadius,
            Vector2 targetCenter,
            float targetRadius,
            float attackRange,
            NavigationMap map,
            out Vector2 position,
            out bool canAttack)
        {
            position = default;
            canAttack = false;
            if (map == null || attackRange <= 0f)
                return false;

            float crowdRadius = Mathf.Max(MinimumCrowdRadius, sourceRadius);
            float targetCrowdRadius = Mathf.Max(MinimumCrowdRadius, targetRadius);
            float reach = sourceRadius + targetRadius + attackRange;
            float attackDistance = Mathf.Min(
                reach - 0.01f,
                Mathf.Max(crowdRadius + targetCrowdRadius + 0.02f, reach - 0.05f));
            if (attackDistance <= 0f)
                return false;

            if (_reservations.TryGetValue(source, out var existing))
            {
                if (existing.Target != target)
                {
                    _reservations.Remove(source);
                }
                else
                {
                    if (!existing.CanAttack
                        && TryFindPosition(source, target, sourceCenter, targetCenter,
                            sourceRadius, crowdRadius, attackDistance, 0, 0, map,
                            out position, out Vector2 promotedOffset))
                    {
                        _reservations[source] = (target, promotedOffset, crowdRadius, true);
                        canAttack = true;
                        return true;
                    }

                    position = targetCenter + existing.Offset;
                    if (map.CanStandAt(position, sourceRadius)
                        && IsFree(source, target, targetCenter, position, crowdRadius)
                        && (!existing.CanAttack || existing.Offset.magnitude <= reach))
                    {
                        canAttack = existing.CanAttack;
                        return true;
                    }

                    _reservations.Remove(source);
                }
            }

            if (!TryFindPosition(source, target, sourceCenter, targetCenter,
                    sourceRadius, crowdRadius, attackDistance, 0, WaitingRings, map,
                    out position, out Vector2 offset))
            {
                return false;
            }

            canAttack = offset.magnitude <= attackDistance + 0.01f;
            _reservations[source] = (target, offset, crowdRadius, canAttack);
            return true;
        }

        public void Release(UnitEntity source)
        {
            _reservations.Remove(source);
        }

        public void Clear()
        {
            _reservations.Clear();
            _releaseBuffer.Clear();
        }

        public void ReleaseTarget(UnitEntity target)
        {
            _releaseBuffer.Clear();
            foreach (var pair in _reservations)
            {
                if (pair.Value.Target == target)
                    _releaseBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _releaseBuffer.Count; i++)
                _reservations.Remove(_releaseBuffer[i]);

            _releaseBuffer.Clear();
        }

        private bool TryFindPosition(
            UnitEntity source,
            UnitEntity target,
            Vector2 sourceCenter,
            Vector2 targetCenter,
            float sourceRadius,
            float crowdRadius,
            float attackDistance,
            int firstRing,
            int lastRing,
            NavigationMap map,
            out Vector2 position,
            out Vector2 offset)
        {
            float facingAngle = Mathf.Atan2(
                sourceCenter.y - targetCenter.y,
                sourceCenter.x - targetCenter.x);
            for (int ring = firstRing; ring <= lastRing; ring++)
            {
                float distance = attackDistance + ring * (crowdRadius * 2f + SlotGap);
                for (int i = 0; i < Directions; i++)
                {
                    int step = i == 0 ? 0 : (i % 2 == 1 ? (i + 1) / 2 : -i / 2);
                    float angle = facingAngle + step * Mathf.PI * 2f / Directions;
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    position = targetCenter + offset;
                    if (map.CanStandAt(position, sourceRadius)
                        && IsFree(source, target, targetCenter, position, crowdRadius))
                    {
                        return true;
                    }
                }
            }

            position = default;
            offset = default;
            return false;
        }

        private bool IsFree(
            UnitEntity source,
            UnitEntity target,
            Vector2 targetCenter,
            Vector2 position,
            float crowdRadius)
        {
            foreach (var pair in _reservations)
            {
                if (pair.Key == source || pair.Value.Target != target)
                    continue;

                Vector2 otherPosition = targetCenter + pair.Value.Offset;
                float requiredDistance = crowdRadius + pair.Value.Radius + SlotGap;
                if ((position - otherPosition).sqrMagnitude < requiredDistance * requiredDistance)
                    return false;
            }

            return true;
        }
    }
}
