using UnityEngine;

namespace GameLogic.Units.Skills
{
    public abstract class SkillConfig : ScriptableObject
    {
        [SerializeField]
        private ESkillSlot _slot;

        public ESkillSlot Slot => _slot;

        internal virtual bool TryValidate(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_slot != ESkillSlot.Primary && _slot != ESkillSlot.Secondary)
            {
                errorMessage = $"技能槽无效：{_slot}。";
                return false;
            }

            return true;
        }

        internal abstract ISkill CreateSkill(in SkillRuntimeContext context);

        protected static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
