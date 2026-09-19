namespace GameLogic.Units
{
    public interface IUnitRelationResolver
    {
        bool TryGetRelation(
            UnitEntity source,
            UnitEntity target,
            out EUnitRelation relation);
    }
}
