using UnityEngine;

namespace GameLogic.Units.Skills
{
    [CreateAssetMenu(
        fileName = "GuardSkillConfig",
        menuName = "Game/Units/Skills/Guard Skill Config")]
    public sealed class GuardSkillConfig : SkillConfig
    {
        [SerializeField]
        [Range(0f, 1f)]
        private float _damageReductionRatio = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float _cooldownSeconds = 0.25f;

        [SerializeField]
        [Min(0f)]
        private float _minimumDurationSeconds = 0.25f;

        [SerializeField]
        private string _animationStateName = "Warrior_Guard_Blue";

        public float DamageReductionRatio => _damageReductionRatio;

        public float CooldownSeconds => _cooldownSeconds;

        public float MinimumDurationSeconds => _minimumDurationSeconds;

        public string AnimationStateName => _animationStateName;

        internal override ISkill CreateSkill(in SkillRuntimeContext context)
        {
            return new GuardSkill(this);
        }
    }
}
