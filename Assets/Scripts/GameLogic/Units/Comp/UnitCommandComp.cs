using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitCommandComp : Comp
    {
        public Vector2 MoveDirection { get; private set; }

        public void SetMoveDirection(Vector2 direction)
        {
            MoveDirection = Vector2.ClampMagnitude(direction, 1f);
        }

        public void Clear()
        {
            MoveDirection = Vector2.zero;
        }
    }
}
