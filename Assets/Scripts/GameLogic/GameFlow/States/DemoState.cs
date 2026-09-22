using System;
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
        private readonly Action _returnToMainMenu;
        private UnitEntity _playerUnit;
        private int _entryVersion;

        public DemoState(
            ISceneModule sceneModule,
            IUnitSystem unitSystem,
            Action returnToMainMenu)
        {
            _sceneModule = sceneModule;
            _unitSystem = unitSystem;
            _returnToMainMenu = returnToMainMenu;
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
                FailEntry(entryVersion, "Demo状态进入失败：缺少场景或单位依赖");
                return;
            }

            while (_sceneModule.IsBusy && IsCurrent(entryVersion))
                await UniTask.Yield();

            if (!IsCurrent(entryVersion))
                return;

            if (!_sceneModule.TryGetScene(DemoSceneAddress, out _)
                && !await _sceneModule.LoadSceneAsync(DemoSceneAddress))
            {
                FailEntry(entryVersion, $"Demo状态加载场景失败：{DemoSceneAddress}");
                return;
            }

            if (!IsCurrent(entryVersion))
                return;

            var spawnRequest = new UnitSpawnRequest(
                WarriorBlueDefinitionAddress,
                Vector3.zero,
                Quaternion.identity,
                1,
                true);
            UnitEntity unit = await _unitSystem.SpawnAsync(spawnRequest);

            if (!IsCurrent(entryVersion))
            {
                if (unit != null)
                    _unitSystem.Despawn(unit);

                return;
            }

            _playerUnit = unit;
            if (_playerUnit == null)
                FailEntry(entryVersion, $"Demo状态生成单位失败：{WarriorBlueDefinitionAddress}");
        }

        private void FailEntry(int entryVersion, string message)
        {
            if (!IsCurrent(entryVersion))
                return;

            Debug.LogError($"{message}；返回主菜单。");
            _returnToMainMenu?.Invoke();
        }

        private bool IsCurrent(int entryVersion)
        {
            return entryVersion == _entryVersion && Machine != null && Machine.IsCurrent<DemoState>();
        }
    }
}
