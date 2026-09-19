#if UNITY_EDITOR
using UnityEngine;

namespace GameLogic.Units.Skills
{
    public sealed class MeleeAttackDebugRange
    {
        public Vector2 Center { get; private set; }

        public Vector2 Size { get; private set; }

        public bool IsHitWindowActive { get; private set; }

        public int LastUpdatedFrame { get; private set; }

        internal void Set(
            Vector2 center,
            Vector2 size,
            bool isHitWindowActive)
        {
            Center = center;
            Size = size;
            IsHitWindowActive = isHitWindowActive;
            LastUpdatedFrame = Time.frameCount;
        }
    }
}
#endif
