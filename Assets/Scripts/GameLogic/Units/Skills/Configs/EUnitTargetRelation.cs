using System;

namespace GameLogic.Units.Skills
{
    [Flags]
    public enum EUnitTargetRelation
    {
        None = 0,
        Self = 1 << 0,
        Ally = 1 << 1,
        Enemy = 1 << 2
    }
}
