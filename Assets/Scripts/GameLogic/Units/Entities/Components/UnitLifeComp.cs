using BorFramework;

namespace GameLogic.Units.Common
{
    internal sealed class UnitLifeComp : Comp
    {
        public bool IsDead { get; private set; }

        public bool TryMarkDead()
        {
            if (IsDead)
                return false;

            IsDead = true;
            return true;
        }
    }
}
