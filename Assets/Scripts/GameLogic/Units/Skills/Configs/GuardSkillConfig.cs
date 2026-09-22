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

        internal override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
                return false;

            if (string.IsNullOrWhiteSpace(_animationStateName))
            {
                errorMessage = "防御技能缺少动画状态名。";
                return false;
            }

            if (!IsFiniteNonNegative(_cooldownSeconds)
                || !IsFiniteNonNegative(_minimumDurationSeconds))
            {
                errorMessage = "防御冷却时间和最短持续时间必须是非负有限数值。";
                return false;
            }

            if (!IsFiniteNonNegative(_damageReductionRatio) || _damageReductionRatio > 1f)
            {
                errorMessage = "防御减伤比例必须在 0 到 1 之间。";
                return false;
            }

            return true;
        }

        internal override ISkill CreateSkill(in SkillRuntimeContext context)
        {
            return new GuardSkill(this);
        }
    }
}
