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
            out Vector2 navigationAnchorOffset,
            out float clearanceRadius)
        {
            navigationAnchorOffset = Vector2.zero;
            clearanceRadius = 0f;
            if (!(BodyCollider is CircleCollider2D circleCollider))
                return false;

            navigationAnchorOffset = BodyCollider.transform.TransformVector(circleCollider.offset);
            Vector3 worldScale = BodyCollider.transform.lossyScale;
            float largestScale = Mathf.Max(
                Mathf.Abs(worldScale.x),
                Mathf.Abs(worldScale.y));
            clearanceRadius = circleCollider.radius * largestScale;
            return true;
        }
    }
}
