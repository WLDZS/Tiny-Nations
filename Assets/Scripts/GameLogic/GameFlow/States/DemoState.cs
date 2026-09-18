using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.Units;
using UnityEngine;

namespace GameLogic.GameFlow.States
{
    public sealed class DemoState : State
    {
        private const string DemoSceneAddress = "Demo";
        private const string WarriorBlueDefinitionAddress = "WarriorBlueDefinition";

        private readonly ISceneModule _sceneModule;
        private readonly IUnitSystem _unitSystem;
        private UnitEntity _playerUnit;
        private int _entryVersion;

        public DemoState(ISceneModule sceneModule, IUnitSystem unitSystem)
        {
            _sceneModule = sceneModule;
            _unitSystem = unitSystem;
        }

        public override void OnEnter()
        {
            _entryVersion++;
            EnterAsync(_entryVersion).Forget();
        }

        public override void OnExit()
        {
            _entryVersion++;

            if (_playerUnit == null)
                return;

            _unitSystem?.Despawn(_playerUnit);
            _playerUnit = null;
        }

        private async UniTask EnterAsync(int entryVersion)
        {
            if (_sceneModule == null || _unitSystem == null)
            {
                Debug.LogError("Demo状态进入失败：缺少SceneModule或UnitSystem");
                return;
            }

            if (!await _sceneModule.LoadSceneAsync(DemoSceneAddress))
            {
                if (IsCurrent(entryVersion))
                    Debug.LogError($"Demo状态加载场景失败：{DemoSceneAddress}");

                return;
            }

            if (!IsCurrent(entryVersion))
                return;

            UnitEntity unit = await _unitSystem.SpawnAsync(
                WarriorBlueDefinitionAddress,
                Vector3.zero,
                Quaternion.identity,
                true);

            if (!IsCurrent(entryVersion))
            {
                if (unit != null)
                    _unitSystem.Despawn(unit);

                return;
            }

            _playerUnit = unit;
            if (_playerUnit == null)
                Debug.LogError($"Demo状态生成单位失败：{WarriorBlueDefinitionAddress}");
        }

        private bool IsCurrent(int entryVersion)
        {
            return entryVersion == _entryVersion && Machine != null && Machine.IsCurrent<DemoState>();
        }
    }
}
