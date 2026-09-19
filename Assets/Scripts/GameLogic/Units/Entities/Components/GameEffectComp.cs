using System.Collections.Generic;
using BorFramework;
using GameLogic.Units.Common;

namespace GameLogic.Units.Effects
{
    internal sealed class GameEffectComp : Comp
    {
        private readonly UnitAttributeComp _attributes;
        private readonly UnitLifeComp _life;
        private readonly IEventModule _eventModule;
        private readonly List<ActiveGameEffect> _activeEffects = new();

        public GameEffectComp(
            UnitAttributeComp attributes,
            UnitLifeComp life,
            IEventModule eventModule)
        {
            _attributes = attributes;
            _life = life;
            _eventModule = eventModule;
        }

        public bool TryApply(GameEffectConfig config, UnitEntity source)
        {
            if (config == null
                || !config.IsValid()
                || _life.IsDead
                || !_attributes.TryGetCurrentValue(
                    EUnitAttributeType.Health,
                    out float health)
                || health <= 0f)
            {
                return false;
            }

            if (config.DurationPolicy == EGameEffectDurationPolicy.Instant)
                return TryApplyDamage(config, source);

            _activeEffects.Add(new ActiveGameEffect(config, source));
            return true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || _activeEffects.Count == 0)
                return;

            if (_life.IsDead
                || !_attributes.TryGetCurrentValue(
                    EUnitAttributeType.Health,
                    out float health)
                || health <= 0f)
            {
                _activeEffects.Clear();
                return;
            }

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                ActiveGameEffect activeEffect = _activeEffects[i];
                int applicationCount = activeEffect.Advance(dt);

                for (int applicationIndex = 0;
                     applicationIndex < applicationCount;
                     applicationIndex++)
                {
                    if (!TryApplyDamage(activeEffect.Config, activeEffect.Source))
                        break;
                }

                if (activeEffect.IsComplete)
                    _activeEffects.RemoveAt(i);
            }
        }

        public void Clear()
        {
            _activeEffects.Clear();
        }

        private bool TryApplyDamage(
            GameEffectConfig config,
            UnitEntity source)
        {
            if (!_attributes.TryChangeResource(
                    EUnitAttributeType.Health,
                    -config.DamagePerApplication,
                    out float actualDelta))
            {
                return false;
            }

            float actualDamage = -actualDelta;
            if (actualDamage <= 0f)
                return false;

            if (Entity is UnitEntity target)
            {
                _eventModule?.Publish(new UnitDamageEvent(
                    source,
                    target,
                    config,
                    config.DamagePerApplication,
                    actualDamage));

                if (_attributes.TryGetCurrentValue(
                        EUnitAttributeType.Health,
                        out float health)
                    && health <= 0f
                    && _life.TryMarkDead())
                {
                    _eventModule?.Publish(new UnitDeathEvent(
                        source,
                        target,
                        config));
                }
            }

            return true;
        }
    }
}
