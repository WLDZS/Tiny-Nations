using System;
using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.MainMenu.UI;
using UnityEngine;

namespace GameLogic.GameFlow.States
{
    public sealed class MainMenuState : State
    {
        private const string MainMenuSceneAddress = "MainMenu";
        private const string MainMenuViewAddress = "MainMenuView";

        private readonly ISceneModule _sceneModule;
        private readonly IUIModule _uiModule;
        private readonly Func<bool> _enterDemo;
        private readonly Func<bool> _enterNavigationTest;
        private int _entryVersion;

        /// <summary>场景和菜单均已准备好，可以接受进入关卡的请求。</summary>
        public bool IsReady { get; private set; }

        public MainMenuState(
            ISceneModule sceneModule,
            IUIModule uiModule,
            Func<bool> enterDemo,
            Func<bool> enterNavigationTest)
        {
            _sceneModule = sceneModule;
            _uiModule = uiModule;
            _enterDemo = enterDemo;
            _enterNavigationTest = enterNavigationTest;
        }

        public override void OnEnter()
        {
            _entryVersion++;
            IsReady = false;
            EnterAsync(_entryVersion).Forget();
        }

        public override void OnExit()
        {
            _entryVersion++;
            IsReady = false;
            _uiModule?.Destroy<MainMenuView>();
        }

        private async UniTask EnterAsync(int entryVersion)
        {
            if (_sceneModule == null || _uiModule == null)
            {
                Debug.LogError("MainMenu状态进入失败：缺少SceneModule或UIModule");
                return;
            }

            // 上一次进入可能仍在结束场景操作，等待它结束再判断当前场景。
            while (_sceneModule.IsBusy && IsCurrent(entryVersion))
                await UniTask.Yield();

            if (!IsCurrent(entryVersion))
                return;

            if (!_sceneModule.TryGetScene(MainMenuSceneAddress, out _)
                && !await _sceneModule.LoadSceneAsync(MainMenuSceneAddress))
            {
                if (IsCurrent(entryVersion))
                    Debug.LogError($"MainMenu状态加载场景失败：{MainMenuSceneAddress}");

                return;
            }

            if (!IsCurrent(entryVersion))
                return;

            _uiModule.Register<MainMenuView, MainMenuViewModel>(
                MainMenuViewAddress,
                EUILayer.Screen,
                () => new MainMenuViewModel(_enterDemo, _enterNavigationTest));

            MainMenuView view = await _uiModule.PushScreenAsync<MainMenuView>();
            if (!IsCurrent(entryVersion))
                return;

            if (view == null)
            {
                Debug.LogError($"MainMenu状态加载界面失败：{MainMenuViewAddress}");
                return;
            }

            IsReady = true;
        }

        private bool IsCurrent(int entryVersion)
        {
            return entryVersion == _entryVersion && Machine != null && Machine.IsCurrent<MainMenuState>();
        }
    }
}
