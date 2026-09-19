using BorFramework;
using GameLogic.Units.Effects;

namespace GameLogic.Units
{
    public readonly struct UnitDamageEvent : IEvent
    {
        public UnitEntity Source { get; }

        public UnitEntity Target { get; }

        public GameEffectConfig GameEffect { get; }

        public float RequestedDamage { get; }

        public float ActualDamage { get; }

        public UnitDamageEvent(
            UnitEntity source,
            UnitEntity target,
            GameEffectConfig gameEffect,
            float requestedDamage,
            float actualDamage)
        {
            Source = source;
            Target = target;
            GameEffect = gameEffect;
            RequestedDamage = requestedDamage;
            ActualDamage = actualDamage;
        }
    }
}
