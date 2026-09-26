using System;
using BorFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic.GameFlow.States
{
    public sealed class NavigationTestState : State
    {
        private const string SceneAddress = "NavigationTest";

        private readonly ISceneModule _sceneModule;
        private readonly Func<NavigationSceneSystems> _createSceneSystems;
        private readonly Action _returnToMainMenu;
        private NavigationSceneSystems _sceneSystems;
        private int _entryVersion;

        public NavigationTestState(
            ISceneModule sceneModule,
            Func<NavigationSceneSystems> createSceneSystems,
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

        public override void OnUpdate(float dt)
        {
            if (_sceneSystems != null
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
                _returnToMainMenu?.Invoke();
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
                FailEntry(entryVersion, "导航测试场景缺少场景或系统依赖");
                return;
            }

            while (_sceneModule.IsBusy && IsCurrent(entryVersion))
                await UniTask.Yield();

            if (!IsCurrent(entryVersion))
                return;

            if (!_sceneModule.TryGetScene(SceneAddress, out _)
                && !await _sceneModule.LoadSceneAsync(SceneAddress))
            {
                FailEntry(entryVersion, $"导航测试场景加载失败：{SceneAddress}");
                return;
            }

            if (!IsCurrent(entryVersion))
                return;

            _sceneSystems = _createSceneSystems();
            if (_sceneSystems == null || !_sceneSystems.Start())
                FailEntry(entryVersion, "导航测试场景系统启动失败");
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
            return entryVersion == _entryVersion && Machine != null && Machine.IsCurrent<NavigationTestState>();
        }
    }
}
