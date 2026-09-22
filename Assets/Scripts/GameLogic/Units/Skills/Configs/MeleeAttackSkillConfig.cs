using System;
using System.Collections.Generic;
using GameLogic.Units.Effects;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    [CreateAssetMenu(
        fileName = "MeleeAttackSkillConfig",
        menuName = "Game/Units/Skills/Melee Attack Skill Config")]
    public sealed class MeleeAttackSkillConfig : SkillConfig
    {
        private const EUnitTargetRelation SupportedTargetRelations =
            EUnitTargetRelation.Self | EUnitTargetRelation.Ally | EUnitTargetRelation.Enemy;

        [SerializeField]
        [Min(0f)]
        private float _cooldownSeconds = 0.25f;

        [SerializeField]
        private Vector2 _querySize = new Vector2(3f, 3f);

        [SerializeField]
        private Vector2 _queryOffset;

        [SerializeField]
        private LayerMask _hitLayerMask = 1 << 7;

        [SerializeField]
        private EUnitTargetRelation _targetRelations = EUnitTargetRelation.Enemy;

        [SerializeField]
        private SkillHitWindow[] _hitWindows = Array.Empty<SkillHitWindow>();

        [SerializeField]
        private GameEffectConfig[] _gameEffects = Array.Empty<GameEffectConfig>();

        [SerializeField]
        private SkillAnimationStage[] _animationStages = Array.Empty<SkillAnimationStage>();

        public float CooldownSeconds => _cooldownSeconds;

        public Vector2 QuerySize => _querySize;

        public Vector2 QueryOffset => _queryOffset;

        public LayerMask HitLayerMask => _hitLayerMask;

        public EUnitTargetRelation TargetRelations => _targetRelations;

        internal IReadOnlyList<SkillHitWindow> HitWindows => _hitWindows;

        internal IReadOnlyList<GameEffectConfig> GameEffects => _gameEffects;

        internal IReadOnlyList<SkillAnimationStage> AnimationStages => _animationStages;

        internal override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
                return false;

            if (!IsFiniteNonNegative(_cooldownSeconds))
            {
                errorMessage = "近战冷却时间必须是非负有限数值。";
                return false;
            }

            if (!IsFiniteNonNegative(_querySize.x)
                || !IsFiniteNonNegative(_querySize.y)
                || _querySize.x <= 0f
                || _querySize.y <= 0f)
            {
                errorMessage = "近战查询框的宽高必须是大于 0 的有限数值。";
                return false;
            }

            if (float.IsNaN(_queryOffset.x)
                || float.IsInfinity(_queryOffset.x)
                || float.IsNaN(_queryOffset.y)
                || float.IsInfinity(_queryOffset.y))
            {
                errorMessage = "近战查询框偏移必须是有限数值。";
                return false;
            }

            if (_hitLayerMask.value == 0)
            {
                errorMessage = "近战命中 LayerMask 不能为空。";
                return false;
            }

            if (_targetRelations == EUnitTargetRelation.None
                || (_targetRelations & ~SupportedTargetRelations) != 0)
            {
                errorMessage = "近战目标关系必须包含有效的 Self、Ally 或 Enemy。";
                return false;
            }

            if (_animationStages == null || _animationStages.Length == 0)
            {
                errorMessage = "近战技能至少需要一个动画阶段。";
                return false;
            }

            float durationSeconds = 0f;
            for (int i = 0; i < _animationStages.Length; i++)
            {
                SkillAnimationStage stage = _animationStages[i];
                if (stage == null
                    || string.IsNullOrWhiteSpace(stage.StateName)
                    || !IsFiniteNonNegative(stage.DurationSeconds)
                    || stage.DurationSeconds <= 0f)
                {
                    errorMessage = $"近战动画阶段无效，索引：{i}；需要状态名和大于 0 的有限持续时间。";
                    return false;
                }

                durationSeconds += stage.DurationSeconds;
            }

            if (float.IsInfinity(durationSeconds))
            {
                errorMessage = "近战动画阶段总时长超出有效范围。";
                return false;
            }

            if (_hitWindows == null || _hitWindows.Length == 0)
            {
                errorMessage = "近战技能至少需要一个命中窗口。";
                return false;
            }

            for (int i = 0; i < _hitWindows.Length; i++)
            {
                SkillHitWindow window = _hitWindows[i];
                if (window == null
                    || !IsFiniteNonNegative(window.StartTimeSeconds)
                    || window.StartTimeSeconds >= durationSeconds
                    || !IsFiniteNonNegative(window.DurationSeconds)
                    || window.DurationSeconds <= 0f)
                {
                    errorMessage = $"近战命中窗口无效，索引：{i}；开始时间必须在技能内，持续时间必须大于 0。";
                    return false;
                }
            }

            if (_gameEffects == null || _gameEffects.Length == 0)
            {
                errorMessage = "近战技能至少需要一个命中效果。";
                return false;
            }

            for (int i = 0; i < _gameEffects.Length; i++)
            {
                GameEffectConfig effect = _gameEffects[i];
                if (effect == null || !effect.IsValid())
                {
                    errorMessage = $"近战命中效果缺失或无效，索引：{i}。";
                    return false;
                }
            }

            return true;
        }

        internal override ISkill CreateSkill(in SkillRuntimeContext context)
        {
            return new MeleeAttackSkill(this, context);
        }
    }
}
