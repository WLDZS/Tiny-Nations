using BorFramework;

namespace GameLogic.Units.Skills
{
    internal sealed class SkillLogic : Logic
    {
        private readonly SkillComp _skills;

        public override ELogicPhase Phase => ELogicPhase.Combat;

        public SkillLogic(SkillComp skills)
        {
            _skills = skills;
        }

        protected override void OnTick(float dt)
        {
            _skills.Tick(dt);
        }

        public override void OnStop()
        {
            _skills.CancelActiveSkill();
        }
    }
}
