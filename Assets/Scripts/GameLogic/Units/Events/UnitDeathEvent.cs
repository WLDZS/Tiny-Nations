using BorFramework;
using GameLogic.Units.Effects;

namespace GameLogic.Units
{
    public readonly struct UnitDeathEvent : IEvent
    {
        public UnitEntity Source { get; }

        public UnitEntity Target { get; }

        public GameEffectConfig KillingEffect { get; }

        public UnitDeathEvent(
            UnitEntity source,
            UnitEntity target,
            GameEffectConfig killingEffect)
        {
            Source = source;
            Target = target;
            KillingEffect = killingEffect;
        }
    }
}
