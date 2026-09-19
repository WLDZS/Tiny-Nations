using System.Collections.Generic;
using BorFramework;

namespace GameLogic.Units.Skills
{
    internal sealed class UnitSkillComp : Comp
    {
        private readonly Dictionary<ESkillSlot, ISkill> _skills = new();

        public ISkill ActiveSkill { get; private set; }

        public bool TryRegister(ESkillSlot slot, ISkill skill)
        {
            if (skill == null)
                return false;

            return _skills.TryAdd(slot, skill);
        }

        public bool TryGetSkill(ESkillSlot slot, out ISkill skill)
        {
            return _skills.TryGetValue(slot, out skill);
        }

        public bool TryTrigger(ESkillSlot slot, in SkillContext context)
        {
            if (ActiveSkill != null)
                return false;

            if (!_skills.TryGetValue(slot, out ISkill skill))
                return false;

            if (!skill.TryStart(context))
                return false;

            ActiveSkill = skill;
            return true;
        }

        public void Release(ESkillSlot slot)
        {
            if (!_skills.TryGetValue(slot, out ISkill skill))
                return;

            if (ActiveSkill != skill)
                return;

            skill.Stop();
            if (!skill.IsActive)
                ActiveSkill = null;
        }

        public void Tick(float dt)
        {
            foreach (ISkill skill in _skills.Values)
            {
                skill.Tick(dt);
            }

            if (ActiveSkill != null && !ActiveSkill.IsActive)
                ActiveSkill = null;
        }

        public void CancelActiveSkill()
        {
            if (ActiveSkill == null)
                return;

            ActiveSkill.Cancel();
            ActiveSkill = null;
        }
    }
}
