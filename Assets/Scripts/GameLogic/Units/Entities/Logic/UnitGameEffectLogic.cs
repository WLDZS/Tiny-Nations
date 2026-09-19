using BorFramework;

namespace GameLogic.Units.Effects
{
    internal sealed class UnitGameEffectLogic : Logic
    {
        private readonly GameEffectComp _effects;

        public override ELogicPhase Phase => ELogicPhase.Combat;

        public UnitGameEffectLogic(GameEffectComp effects)
        {
            _effects = effects;
        }

        protected override void OnTick(float dt)
        {
            _effects.Tick(dt);
        }

        public override void OnStop()
        {
            _effects.Clear();
        }
    }
}
