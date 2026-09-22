namespace GameLogic.Units.Skills
{
    internal readonly struct SkillContext
    {
        public UnitEntity Target { get; }

        public bool HasTarget => Target != null;

        public SkillContext(UnitEntity target)
        {
            Target = target;
        }
    }
}
