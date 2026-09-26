#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    public static class MeleeAttackDebugRangeRegistry
    {
        private static readonly Dictionary<int, MeleeAttackDebugRange> Ranges = new();
        private static int _nextId;

        public static IReadOnlyDictionary<int, MeleeAttackDebugRange> ActiveRanges => Ranges;

        public static bool HasActiveRanges => Ranges.Count > 0;

        public static int CreateId()
        {
            _nextId++;
            return _nextId;
        }

        public static void Set(
            int id,
            Vector2 center,
            float radius,
            bool isHitWindowActive)
        {
            if (!Ranges.TryGetValue(id, out MeleeAttackDebugRange range))
            {
                range = new MeleeAttackDebugRange();
                Ranges.Add(id, range);
            }

            range.Set(center, radius, isHitWindowActive);
        }

        public static void Remove(int id)
        {
            Ranges.Remove(id);
        }

        public static void Clear()
        {
            Ranges.Clear();
        }
    }
}
#endif
