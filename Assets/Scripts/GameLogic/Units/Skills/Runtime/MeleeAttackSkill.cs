using System.Collections.Generic;
using GameLogic.Units.Effects;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    internal sealed class MeleeAttackSkill : ISkill
    {
        private readonly MeleeAttackSkillConfig _config;
        private readonly SkillRuntimeContext _runtimeContext;
        private readonly int[] _animationStateIds;
        private readonly float[] _stageEndTimesSeconds;
        private readonly float[] _hitWindowStartTimesSeconds;
        private readonly float[] _hitWindowEndTimesSeconds;
        private readonly HashSet<UnitEntity>[] _hitUnitsByWindow;
        private readonly List<Collider2D> _overlapResults = new();
        private readonly float _durationSeconds;
#if UNITY_EDITOR
        private readonly int _debugRangeId;
#endif
        private float _activeTimeRemainingSeconds;
        private float _activeTimeElapsedSeconds;
        private float _cooldownTimeRemainingSeconds;
        private int _animationStageIndex;

        public int AnimationStateId { get; private set; }

        public int AnimationVersion { get; private set; }

        public bool BlocksMovement => true;

        public bool IsActive => _activeTimeRemainingSeconds > 0f;

        public MeleeAttackSkill(
            MeleeAttackSkillConfig config,
            in SkillRuntimeContext runtimeContext)
        {
            _config = config;
            _runtimeContext = runtimeContext;

            IReadOnlyList<SkillAnimationStage> stages = config.AnimationStages;
            _animationStateIds = new int[stages.Count];
            _stageEndTimesSeconds = new float[stages.Count];
            float durationSeconds = 0f;

            for (int i = 0; i < stages.Count; i++)
            {
                SkillAnimationStage stage = stages[i];
                durationSeconds += stage.DurationSeconds;
                _animationStateIds[i] = Animator.StringToHash(stage.StateName);
                _stageEndTimesSeconds[i] = durationSeconds;
            }

            _durationSeconds = durationSeconds;
            AnimationStateId = _animationStateIds[0];

            IReadOnlyList<SkillHitWindow> hitWindows = config.HitWindows;
            _hitWindowStartTimesSeconds = new float[hitWindows.Count];
            _hitWindowEndTimesSeconds = new float[hitWindows.Count];
            _hitUnitsByWindow = new HashSet<UnitEntity>[hitWindows.Count];

            for (int i = 0; i < hitWindows.Count; i++)
            {
                SkillHitWindow hitWindow = hitWindows[i];
                _hitWindowStartTimesSeconds[i] = hitWindow.StartTimeSeconds;
                _hitWindowEndTimesSeconds[i] = Mathf.Min(
                    hitWindow.StartTimeSeconds + hitWindow.DurationSeconds,
                    _durationSeconds);
                _hitUnitsByWindow[i] = new HashSet<UnitEntity>();
            }

#if UNITY_EDITOR
            _debugRangeId = MeleeAttackDebugRangeRegistry.CreateId();
#endif
        }

        public bool IsTargetInRange(in SkillContext context)
        {
            if (!context.HasTarget || !CanQueryAttackArea())
                return false;

            QueryAttackArea();
            for (int i = 0; i < _overlapResults.Count; i++)
            {
                Collider2D hitCollider = _overlapResults[i];
                if (_runtimeContext.UnitQuery.TryGetUnit(
                        hitCollider,
                        out UnitEntity target)
                    && target == context.Target)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanTrigger(in SkillContext context)
        {
            if (_animationStateIds.Length == 0 || IsActive || _cooldownTimeRemainingSeconds > 0f)
                return false;

            return !context.HasTarget || IsTargetInRange(context);
        }

        public bool TryStart(in SkillContext context)
        {
            if (!CanTrigger(context))
                return false;

            _activeTimeRemainingSeconds = _durationSeconds;
            _activeTimeElapsedSeconds = 0f;
            _cooldownTimeRemainingSeconds = _config.CooldownSeconds;
            _animationStageIndex = 0;
            AnimationStateId = _animationStateIds[0];
            AnimationVersion++;

            for (int i = 0; i < _hitUnitsByWindow.Length; i++)
                _hitUnitsByWindow[i].Clear();

            return true;
        }

        public void Tick(float dt)
        {
            if (_activeTimeRemainingSeconds > 0f)
            {
                float previousElapsedSeconds = _activeTimeElapsedSeconds;
                _activeTimeElapsedSeconds = Mathf.Min(
                    _durationSeconds,
                    _activeTimeElapsedSeconds + Mathf.Max(0f, dt));
                _activeTimeRemainingSeconds = Mathf.Max(
                    0f,
                    _durationSeconds - _activeTimeElapsedSeconds);

                QueryHitWindows(previousElapsedSeconds, _activeTimeElapsedSeconds);
                DrawQueryRange(IsInsideHitWindow(_activeTimeElapsedSeconds));

                while (_animationStageIndex + 1 < _animationStateIds.Length
                       && _activeTimeElapsedSeconds >= _stageEndTimesSeconds[_animationStageIndex])
                {
                    _animationStageIndex++;
                    AnimationStateId = _animationStateIds[_animationStageIndex];
                    AnimationVersion++;
                }
            }

            if (_cooldownTimeRemainingSeconds > 0f)
                _cooldownTimeRemainingSeconds = Mathf.Max(0f, _cooldownTimeRemainingSeconds - dt);
        }

        public void Stop()
        {
            _activeTimeRemainingSeconds = 0f;

#if UNITY_EDITOR
            MeleeAttackDebugRangeRegistry.Remove(_debugRangeId);
#endif
        }

        public void Cancel()
        {
            Stop();
        }

        private void QueryHitWindows(
            float previousElapsedSeconds,
            float currentElapsedSeconds)
        {
            if (!CanQueryAttackArea())
                return;

            for (int i = 0; i < _hitWindowStartTimesSeconds.Length; i++)
            {
                if (currentElapsedSeconds < _hitWindowStartTimesSeconds[i]
                    || previousElapsedSeconds > _hitWindowEndTimesSeconds[i])
                {
                    continue;
                }

                QueryHitWindow(i);
            }
        }

        private void QueryHitWindow(int hitWindowIndex)
        {
            QueryAttackArea();

            HashSet<UnitEntity> hitUnits = _hitUnitsByWindow[hitWindowIndex];
            for (int i = 0; i < _overlapResults.Count; i++)
            {
                Collider2D hitCollider = _overlapResults[i];
                if (!_runtimeContext.UnitQuery.TryGetUnit(hitCollider, out UnitEntity target)
                    || target == null
                    || hitUnits.Contains(target)
                    || !CanAffectTarget(target)
                    || !target.Attributes.TryGetCurrentValue(
                        EUnitAttributeType.Health,
                        out float health)
                    || health <= 0f)
                {
                    continue;
                }

                hitUnits.Add(target);
                ApplyGameEffects(target);
            }
        }

        private bool CanQueryAttackArea()
        {
            return _config.QuerySize.x > 0f
                   && _config.QuerySize.y > 0f
                   && _config.HitLayerMask.value != 0
                   && _runtimeContext.UnitQuery != null;
        }

        private void QueryAttackArea()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(_config.HitLayerMask);
            filter.useTriggers = true;

            _overlapResults.Clear();
            Physics2D.OverlapBox(
                GetQueryCenter(),
                _config.QuerySize,
                0f,
                filter,
                _overlapResults);
        }

        private bool CanAffectTarget(UnitEntity target)
        {
            if (_runtimeContext.RelationResolver == null
                || !_runtimeContext.RelationResolver.TryGetRelation(
                    _runtimeContext.Owner,
                    target,
                    out EUnitRelation relation))
            {
                return false;
            }

            EUnitTargetRelation targetRelation;
            switch (relation)
            {
                case EUnitRelation.Self:
                    targetRelation = EUnitTargetRelation.Self;
                    break;
                case EUnitRelation.Ally:
                    targetRelation = EUnitTargetRelation.Ally;
                    break;
                case EUnitRelation.Enemy:
                    targetRelation = EUnitTargetRelation.Enemy;
                    break;
                default:
                    return false;
            }

            return (_config.TargetRelations & targetRelation) != 0;
        }

        private void ApplyGameEffects(UnitEntity target)
        {
            IReadOnlyList<GameEffectConfig> gameEffects = _config.GameEffects;
            for (int i = 0; i < gameEffects.Count; i++)
            {
                GameEffectConfig gameEffect = gameEffects[i];
                if (gameEffect != null)
                    target.Effects.TryApply(gameEffect, _runtimeContext.Owner);
            }
        }

        private bool IsInsideHitWindow(float elapsedSeconds)
        {
            for (int i = 0; i < _hitWindowStartTimesSeconds.Length; i++)
            {
                if (elapsedSeconds >= _hitWindowStartTimesSeconds[i]
                    && elapsedSeconds <= _hitWindowEndTimesSeconds[i])
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 GetQueryCenter()
        {
            Vector2 queryOffset = _config.QueryOffset;
            queryOffset.x *= _runtimeContext.HorizontalFacingSign;

            return (Vector2)_runtimeContext.OwnerWorldPositionTransform.position + queryOffset;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DrawQueryRange(bool isHitWindowActive)
        {
#if UNITY_EDITOR
            if (_runtimeContext.OwnerWorldPositionTransform == null
                || _config.QuerySize.x <= 0f
                || _config.QuerySize.y <= 0f)
            {
                MeleeAttackDebugRangeRegistry.Remove(_debugRangeId);
                return;
            }

            MeleeAttackDebugRangeRegistry.Set(
                _debugRangeId,
                GetQueryCenter(),
                _config.QuerySize,
                isHitWindowActive);
#endif
        }
    }
}
