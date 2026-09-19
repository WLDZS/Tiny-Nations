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
        [SerializeField]
        [Min(0f)]
        private float _triggerRange = 1.5f;

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

        public float TriggerRange => _triggerRange;

        public float CooldownSeconds => _cooldownSeconds;

        public Vector2 QuerySize => _querySize;

        public Vector2 QueryOffset => _queryOffset;

        public LayerMask HitLayerMask => _hitLayerMask;

        public EUnitTargetRelation TargetRelations => _targetRelations;

        internal IReadOnlyList<SkillHitWindow> HitWindows => _hitWindows;

        internal IReadOnlyList<GameEffectConfig> GameEffects => _gameEffects;

        internal IReadOnlyList<SkillAnimationStage> AnimationStages => _animationStages;

        internal override ISkill CreateSkill(in SkillRuntimeContext context)
        {
            return new MeleeAttackSkill(this, context);
        }
    }
}
