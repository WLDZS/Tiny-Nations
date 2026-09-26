namespace GameLogic.Units.Skills
{
    internal readonly struct SkillRuntimeContext
    {
        public UnitEntity Owner { get; }

        public IUnitQuery UnitQuery { get; }

        public IUnitRelationResolver RelationResolver { get; }

        public SkillRuntimeContext(
            UnitEntity owner,
            IUnitQuery unitQuery,
            IUnitRelationResolver relationResolver)
        {
            Owner = owner;
            UnitQuery = unitQuery;
            RelationResolver = relationResolver;
        }
    }
}
