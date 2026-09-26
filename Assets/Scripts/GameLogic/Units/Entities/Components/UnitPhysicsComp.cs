using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitPhysicsComp : Comp
    {
        public Rigidbody2D Rigidbody { get; }

        public Collider2D BodyCollider { get; }

        public UnitPhysicsComp(
            Rigidbody2D rigidbody,
            Collider2D bodyCollider)
        {
            Rigidbody = rigidbody;
            BodyCollider = bodyCollider;
        }

        public bool TryGetNavigationGeometry(
            Transform worldPositionTransform,
            out Vector2 navigationAnchorOffset,
            out float clearanceRadius)
        {
            navigationAnchorOffset = Vector2.zero;
            clearanceRadius = 0f;
            if (worldPositionTransform == null
                || !(BodyCollider is CircleCollider2D circleCollider))
                return false;

            Vector2 colliderCenter = BodyCollider.transform.TransformPoint(circleCollider.offset);
            navigationAnchorOffset = colliderCenter - (Vector2)worldPositionTransform.position;
            Vector3 worldScale = BodyCollider.transform.lossyScale;
            float largestScale = Mathf.Max(
                Mathf.Abs(worldScale.x),
                Mathf.Abs(worldScale.y));
            clearanceRadius = circleCollider.radius * largestScale;
            return true;
        }
    }
}
