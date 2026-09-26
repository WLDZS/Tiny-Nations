using System.Collections.Generic;
using GameLogic.Navigation;
using UnityEngine;

namespace GameLogic.Units
{
    /// <summary>Keeps stable, reachable positions around a shared melee target.</summary>
    internal sealed class UnitApproachSlots
    {
        private const int InnerSlotCount = 2;
        private const int OuterSlotsPerRing = 12;
        private const int TotalSlotCount = InnerSlotCount + OuterSlotsPerRing * 2;
        private const float RevalidateDistance = 0.4f;

        private readonly Dictionary<UnitEntity, Reservation> _reservations = new();
        private readonly List<Vector2> _candidatePath = new();

        public bool TryGetPosition(
            UnitEntity source,
            UnitRuntime sourceRuntime,
            UnitEntity target,
            UnitRuntime targetRuntime,
            INavigationSystem navigationSystem,
            out Vector3 position)
        {
            position = default;
            if (sourceRuntime.WorldPositionTransform == null
                || targetRuntime.WorldPositionTransform == null
                || navigationSystem == null)
            {
                Release(source);
                return false;
            }

            Vector2 sourcePosition = sourceRuntime.WorldPositionTransform.position;
            Vector2 targetPosition = targetRuntime.WorldPositionTransform.position;
            Vector2 sourceOffset = Vector2.zero;
            Vector2 targetOffset = Vector2.zero;
            float sourceRadius = 0f;
            float targetRadius = 0f;
            if (source.Physics != null
                && !source.Physics.TryGetNavigationGeometry(
                    sourceRuntime.WorldPositionTransform, out sourceOffset, out sourceRadius))
            {
                Release(source);
                return false;
            }

            if (target.Physics != null
                && !target.Physics.TryGetNavigationGeometry(
                    targetRuntime.WorldPositionTransform, out targetOffset, out targetRadius))
            {
                Release(source);
                return false;
            }

            Vector2 targetCenter = targetPosition + targetOffset;
            if (_reservations.TryGetValue(source, out Reservation reservation)
                && reservation.Target != target)
            {
                Release(source);
            }

            if (!_reservations.ContainsKey(source))
                _reservations[source] = new Reservation(target, -1, targetCenter);

            if (CountPursuers(target) == 1)
            {
                _reservations[source] = new Reservation(target, -1, targetCenter);
                position = targetPosition;
                return true;
            }

            reservation = _reservations[source];
            if (reservation.SlotIndex >= 0)
            {
                Vector2 reservedPosition = GetSlotPosition(
                    reservation.SlotIndex, targetCenter, sourceOffset, sourceRadius, targetRadius);
                bool needsValidation = (targetCenter - reservation.ValidatedTargetCenter).sqrMagnitude
                                       >= RevalidateDistance * RevalidateDistance;
                if (needsValidation && !CanReach(
                        navigationSystem, sourcePosition, reservedPosition, sourceOffset, sourceRadius))
                {
                    _reservations[source] = new Reservation(target, -1, targetCenter);
                }
                else
                {
                    if (needsValidation)
                        _reservations[source] = new Reservation(target, reservation.SlotIndex, targetCenter);

                    if (reservation.SlotIndex >= InnerSlotCount
                        && TryFindSlot(source, target, sourcePosition, targetCenter,
                            sourceOffset, sourceRadius, targetRadius, navigationSystem,
                            0, InnerSlotCount, out int freeInnerSlot))
                    {
                        _reservations[source] = new Reservation(target, freeInnerSlot, targetCenter);
                        reservedPosition = GetSlotPosition(
                            freeInnerSlot, targetCenter, sourceOffset, sourceRadius, targetRadius);
                    }

                    position = reservedPosition;
                    return true;
                }
            }

            bool foundInner = TryFindSlot(source, target, sourcePosition, targetCenter,
                sourceOffset, sourceRadius, targetRadius, navigationSystem,
                0, InnerSlotCount, out int slotIndex);
            if (!foundInner
                && !TryFindSlot(source, target, sourcePosition, targetCenter,
                    sourceOffset, sourceRadius, targetRadius, navigationSystem,
                    InnerSlotCount, TotalSlotCount, out slotIndex))
            {
                return false;
            }

            _reservations[source] = new Reservation(target, slotIndex, targetCenter);
            position = GetSlotPosition(slotIndex, targetCenter, sourceOffset, sourceRadius, targetRadius);
            return true;
        }

        public void Release(UnitEntity source)
        {
            if (source != null)
                _reservations.Remove(source);
        }

        public bool TryGetSlotIndex(UnitEntity source, out int slotIndex)
        {
            slotIndex = -1;
            if (source == null
                || !_reservations.TryGetValue(source, out Reservation reservation)
                || reservation.SlotIndex < 0)
                return false;

            slotIndex = reservation.SlotIndex;
            return true;
        }

        public void Clear()
        {
            _reservations.Clear();
            _candidatePath.Clear();
        }

        private int CountPursuers(UnitEntity target)
        {
            int count = 0;
            foreach (Reservation reservation in _reservations.Values)
            {
                if (reservation.Target == target)
                    count++;
            }

            return count;
        }

        private bool TryFindSlot(
            UnitEntity source,
            UnitEntity target,
            Vector2 sourcePosition,
            Vector2 targetCenter,
            Vector2 sourceOffset,
            float sourceRadius,
            float targetRadius,
            INavigationSystem navigationSystem,
            int firstSlot,
            int endSlot,
            out int slotIndex)
        {
            slotIndex = -1;
            uint rejectedSlots = 0;
            while (true)
            {
                float nearestDistanceSquared = float.PositiveInfinity;
                int nearestSlot = -1;
                for (int index = firstSlot; index < endSlot; index++)
                {
                    if ((rejectedSlots & (1u << index)) != 0 || IsReservedByOther(source, target, index))
                        continue;

                    Vector2 candidate = GetSlotPosition(
                        index, targetCenter, sourceOffset, sourceRadius, targetRadius);
                    float distanceSquared = (candidate - sourcePosition).sqrMagnitude;
                    if (distanceSquared >= nearestDistanceSquared)
                        continue;

                    nearestDistanceSquared = distanceSquared;
                    nearestSlot = index;
                }

                if (nearestSlot < 0)
                    return false;

                Vector2 nearestPosition = GetSlotPosition(
                    nearestSlot, targetCenter, sourceOffset, sourceRadius, targetRadius);
                if (CanReach(navigationSystem, sourcePosition, nearestPosition, sourceOffset, sourceRadius))
                {
                    slotIndex = nearestSlot;
                    return true;
                }

                rejectedSlots |= 1u << nearestSlot;
            }
        }

        private bool IsReservedByOther(UnitEntity source, UnitEntity target, int slotIndex)
        {
            foreach (KeyValuePair<UnitEntity, Reservation> pair in _reservations)
            {
                if (pair.Key != source
                    && pair.Value.Target == target
                    && pair.Value.SlotIndex == slotIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanReach(
            INavigationSystem navigationSystem,
            Vector2 sourcePosition,
            Vector2 destination,
            Vector2 sourceOffset,
            float sourceRadius)
        {
            return navigationSystem.FindPath(
                sourcePosition, destination, sourceOffset, sourceRadius, _candidatePath)
                   == EPathQueryStatus.Success;
        }

        private static Vector2 GetSlotPosition(
            int slotIndex,
            Vector2 targetCenter,
            Vector2 sourceOffset,
            float sourceRadius,
            float targetRadius)
        {
            if (slotIndex < InnerSlotCount)
            {
                float sideDistance = Mathf.Max(0.7f, sourceRadius + targetRadius + 0.2f);
                float rearDistance = Mathf.Max(0.6f, sourceRadius + targetRadius + 0.15f);
                float side = slotIndex == 0 ? -1f : 1f;
                return targetCenter + new Vector2(side * sideDistance, -rearDistance) - sourceOffset;
            }

            int outerIndex = slotIndex - InnerSlotCount;
            int ring = outerIndex / OuterSlotsPerRing;
            int angleIndex = outerIndex % OuterSlotsPerRing;
            float spacing = sourceRadius * 2f + 0.3f;
            float innerRadius = Mathf.Max(1.6f, spacing / (2f * Mathf.Sin(Mathf.PI / OuterSlotsPerRing)));
            float ringRadius = innerRadius + ring * Mathf.Max(0.9f, spacing);
            float angle = (angleIndex + ring * 0.5f) * Mathf.PI * 2f / OuterSlotsPerRing;
            return targetCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius
                   - sourceOffset;
        }

        private readonly struct Reservation
        {
            public UnitEntity Target { get; }
            public int SlotIndex { get; }
            public Vector2 ValidatedTargetCenter { get; }

            public Reservation(UnitEntity target, int slotIndex, Vector2 validatedTargetCenter)
            {
                Target = target;
                SlotIndex = slotIndex;
                ValidatedTargetCenter = validatedTargetCenter;
            }
        }
    }
}
