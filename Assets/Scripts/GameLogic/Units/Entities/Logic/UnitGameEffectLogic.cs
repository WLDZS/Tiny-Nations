using BorFramework;
using GameLogic.Units.Common;

namespace GameLogic.Units.Effects
{
    internal sealed class UnitGameEffectLogic : Logic
    {
        private readonly UnitEntity _owner;
        private readonly GameEffectComp _effects;
        private readonly UnitAttributeComp _attributes;
        private readonly UnitLifeComp _life;
        private readonly IEventModule _eventModule;

        public override ELogicPhase Phase => ELogicPhase.Combat;

        public UnitGameEffectLogic(
            UnitEntity owner,
            GameEffectComp effects,
            UnitAttributeComp attributes,
            UnitLifeComp life,
            IEventModule eventModule)
        {
            _owner = owner;
            _effects = effects;
            _attributes = attributes;
            _life = life;
            _eventModule = eventModule;
        }

        protected override void OnTick(float dt)
        {
            if (_life.IsDead)
            {
                _effects.Clear();
                return;
            }

            if (dt <= 0f)
                return;

            for (int i = _effects.ActiveEffects.Count - 1; i >= 0; i--)
            {
                ActiveGameEffect effect = _effects.ActiveEffects[i];
                int applicationCount = effect.Advance(dt);
                for (int applicationIndex = 0; applicationIndex < applicationCount; applicationIndex++)
                {
                    if (!TryApplyDamage(effect.Config, effect.Source))
                        break;
                }

                if (_life.IsDead)
                {
                    _effects.Clear();
                    return;
                }

                if (effect.IsComplete)
                    _effects.RemoveAt(i);
            }
        }

        protected override void OnStop()
        {
            _effects.Clear();
        }

        protected override void OnDispose()
        {
            _effects.Clear();
        }

        public bool TryApply(GameEffectConfig config, UnitEntity source)
        {
            if (_owner.IsDisposed || _life.IsDead || config == null || !config.IsValid())
                return false;

            if (config.DurationPolicy == EGameEffectDurationPolicy.Instant)
                return TryApplyDamage(config, source);

            _effects.Add(new ActiveGameEffect(config, source));
            return true;
        }

        private bool TryApplyDamage(GameEffectConfig config, UnitEntity source)
        {
            if (_life.IsDead
                || !_attributes.TryChangeResource(
                    EUnitAttributeType.Health,
                    -config.DamagePerApplication,
                    out float actualDelta)
                || actualDelta >= 0f)
            {
                return false;
            }

            _attributes.TryGetCurrentValue(EUnitAttributeType.Health, out float health);
            bool died = health <= 0f && _life.TryMarkDead();
            _eventModule?.Publish(new UnitDamageEvent(
                source,
                _owner,
                config,
                config.DamagePerApplication,
                -actualDelta));

            if (died)
                _eventModule?.Publish(new UnitDeathEvent(source, _owner, config));

            return true;
        }
    }
}
