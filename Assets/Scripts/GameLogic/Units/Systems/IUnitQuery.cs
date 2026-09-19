using UnityEngine;

namespace GameLogic.Units
{
    public interface IUnitQuery
    {
        bool TryGetUnit(Collider2D collider, out UnitEntity unit);

    }
}
