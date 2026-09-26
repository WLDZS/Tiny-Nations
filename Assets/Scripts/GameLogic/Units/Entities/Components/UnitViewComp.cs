using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitViewComp : Comp
    {
        public Transform Transform { get; }

        public Transform WorldPositionTransform { get; }

        public Animator Animator { get; }

        public SpriteRenderer SpriteRenderer { get; }

        public int CurrentAnimationStateId { get; set; }

        public UnitViewComp(
            Transform transform,
            Transform worldPositionTransform,
            Animator animator,
            SpriteRenderer spriteRenderer)
        {
            Transform = transform;
            WorldPositionTransform = worldPositionTransform;
            Animator = animator;
            SpriteRenderer = spriteRenderer;
        }
    }
}
