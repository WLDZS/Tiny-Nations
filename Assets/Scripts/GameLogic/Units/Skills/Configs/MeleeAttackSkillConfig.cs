using System;
using System.Collections.Generic;
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
        private SkillAnimationStage[] _animationStages = Array.Empty<SkillAnimationStage>();

        public float TriggerRange => _triggerRange;

        public float CooldownSeconds => _cooldownSeconds;

        internal IReadOnlyList<SkillAnimationStage> AnimationStages => _animationStages;

        internal override ISkill CreateSkill()
        {
            return new MeleeAttackSkill(this);
        }
    }
}
