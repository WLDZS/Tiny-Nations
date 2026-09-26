using System;
using BorFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.GameFlow.States
{
    public sealed class DemoState : State
    {
        private const string DemoSceneAddress = "Demo";

        private readonly ISceneModule _sceneModule;
        private readonly Func<UnitSceneSystems> _createSceneSystems;
        private readonly Action _returnToMainMenu;
        private UnitSceneSystems _sceneSystems;
        private int _entryVersion;

        public DemoState(
            ISceneModule sceneModule,
            Func<UnitSceneSystems> createSceneSystems,
            Action returnToMainMenu)
        {
            _sceneModule = sceneModule;
            _createSceneSystems = createSceneSystems;
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

            _sceneSystems?.Dispose();
            _sceneSystems = null;
        }

        private async UniTask EnterAsync(int entryVersion)
        {
            if (_sceneModule == null || _createSceneSystems == null)
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

            if (!_sceneModule.TryGetScene(DemoSceneAddress, out Scene scene))
            {
                FailEntry(entryVersion, $"Demo状态无法获取已加载场景：{DemoSceneAddress}");
                return;
            }

            _sceneSystems = _createSceneSystems();
            if (_sceneSystems == null || !_sceneSystems.Start(scene))
            {
                FailEntry(entryVersion, "Demo场景系统启动失败");
                return;
            }
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
