#if UNITY_EDITOR
using UnityEngine;

namespace GameLogic.Units.Skills
{
    public sealed class MeleeAttackDebugRange
    {
        public Vector2 Center { get; private set; }

        public float Radius { get; private set; }

        public bool IsHitWindowActive { get; private set; }

        public int LastUpdatedFrame { get; private set; }

        internal void Set(
            Vector2 center,
            float radius,
            bool isHitWindowActive)
        {
            Center = center;
            Radius = radius;
            IsHitWindowActive = isHitWindowActive;
            LastUpdatedFrame = Time.frameCount;
        }
    }
}
#endif
