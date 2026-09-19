using BorFramework;
using Cysharp.Threading.Tasks;

namespace GameLogic.Units
{
    public interface IUnitSystem : IGameSystem, IUnitQuery, IUnitRelationResolver
    {
        UniTask<UnitEntity> SpawnAsync(UnitSpawnRequest request);

        bool Despawn(UnitEntity unit);
    }
}
