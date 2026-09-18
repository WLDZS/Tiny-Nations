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
    }
}
