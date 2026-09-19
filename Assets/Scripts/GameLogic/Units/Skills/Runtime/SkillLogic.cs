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

            _skills.Tick(dt);
        }

        public override void OnStop()
        {
            _skills.CancelActiveSkill();
        }
    }
}
