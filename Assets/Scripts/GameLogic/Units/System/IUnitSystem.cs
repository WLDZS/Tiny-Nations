using BorFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameLogic.Units
{
    public interface IUnitSystem : IGameSystem
    {
        UniTask<UnitEntity> SpawnAsync(
            string definitionAddress,
            Vector3 position,
            Quaternion rotation,
            bool usePlayerInput);

        bool Despawn(UnitEntity unit);
    }
}
