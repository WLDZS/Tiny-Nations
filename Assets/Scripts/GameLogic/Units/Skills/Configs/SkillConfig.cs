using UnityEngine;

namespace GameLogic.Units.Skills
{
    public abstract class SkillConfig : ScriptableObject
    {
        [SerializeField]
        private ESkillSlot _slot;

        public ESkillSlot Slot => _slot;

        internal abstract ISkill CreateSkill(in SkillRuntimeContext context);
    }
}
