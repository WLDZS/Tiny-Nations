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
        private int _entryVersion;

        public MainMenuState(ISceneModule sceneModule, IUIModule uiModule, Func<bool> enterDemo)
        {
            _sceneModule = sceneModule;
            _uiModule = uiModule;
            _enterDemo = enterDemo;
        }

        public override void OnEnter()
        {
            _entryVersion++;
            EnterAsync(_entryVersion).Forget();
        }

        public override void OnExit()
        {
            _entryVersion++;
            _uiModule?.Destroy<MainMenuView>();
        }

        private async UniTask EnterAsync(int entryVersion)
        {
            if (_sceneModule == null || _uiModule == null)
            {
                Debug.LogError("MainMenu状态进入失败：缺少SceneModule或UIModule");
                return;
            }

            if (!await _sceneModule.LoadSceneAsync(MainMenuSceneAddress))
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
                () => new MainMenuViewModel(_enterDemo));

            MainMenuView view = await _uiModule.PushScreenAsync<MainMenuView>();
            if (IsCurrent(entryVersion) && view == null)
                Debug.LogError($"MainMenu状态加载界面失败：{MainMenuViewAddress}");
        }

        private bool IsCurrent(int entryVersion)
        {
            return entryVersion == _entryVersion && Machine != null && Machine.IsCurrent<MainMenuState>();
        }
    }
}
