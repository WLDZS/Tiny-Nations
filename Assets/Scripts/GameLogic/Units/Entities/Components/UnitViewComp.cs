using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitViewComp : Comp
    {
        public Transform Transform { get; }

        public Animator Animator { get; }

        public SpriteRenderer SpriteRenderer { get; }

        public float HorizontalFacingSign => SpriteRenderer != null && SpriteRenderer.flipX
            ? -1f
            : 1f;

        public int CurrentAnimationStateId { get; set; }

        public UnitViewComp(
            Transform transform,
            Animator animator,
            SpriteRenderer spriteRenderer)
        {
            Transform = transform;
            Animator = animator;
            SpriteRenderer = spriteRenderer;
        }
    }
}
