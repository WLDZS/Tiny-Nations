using UnityEngine;

namespace GameLogic.Navigation
{
    public readonly struct NavigationCell
    {
        public Vector3Int Position { get; }

        public bool HasGround { get; }

        public bool HasCollision { get; }

        public bool IsWalkable => HasGround && !HasCollision;

        public NavigationCell(Vector3Int position, bool hasGround, bool hasCollision)
        {
            Position = position;
            HasGround = hasGround;
            HasCollision = hasCollision;
        }
    }
}
