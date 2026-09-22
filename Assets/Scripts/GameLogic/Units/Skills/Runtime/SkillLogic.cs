using BorFramework;
using GameLogic.Units.Common;

namespace GameLogic.Units.Skills
{
    internal sealed class SkillLogic : Logic
    {
        private readonly UnitSkillComp _skills;
        private readonly UnitLifeComp _life;

        public override ELogicPhase Phase => ELogicPhase.Combat;

        public SkillLogic(UnitSkillComp skills, UnitLifeComp life)
        {
            _skills = skills;
            _life = life;
        }

        protected override void OnTick(float dt)
        {
            if (_life.IsDead)
            {
                _skills.CancelActiveSkill();
                return;
            }

            foreach (ISkill skill in _skills.RegisteredSkills)
                skill.Tick(dt);

            _skills.RefreshActiveSkill();
        }

        protected override void OnStop()
        {
            _skills.CancelActiveSkill();
        }

        protected override void OnDispose()
        {
            _skills.CancelActiveSkill();
        }
    }
}
