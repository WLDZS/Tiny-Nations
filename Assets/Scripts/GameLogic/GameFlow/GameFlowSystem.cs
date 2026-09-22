using BorFramework;
using GameLogic.GameFlow.States;
using GameLogic.Units;
using UnityEngine;

namespace GameLogic.GameFlow
{
    public sealed class GameFlowSystem : IGameSystem
    {
        private readonly ISceneModule _sceneModule;
        private readonly IUIModule _uiModule;
        private readonly IMonoModule _monoModule;
        private readonly IUnitSystem _unitSystem;
        private readonly StateMachine _stateMachine = new();
        private MainMenuState _mainMenuState;
        private bool _initialized;
        private bool _started;

        public GameFlowSystem(
            ISceneModule sceneModule,
            IUIModule uiModule,
            IMonoModule monoModule,
            IUnitSystem unitSystem)
        {
            _sceneModule = sceneModule;
            _uiModule = uiModule;
            _monoModule = monoModule;
            _unitSystem = unitSystem;
        }

        public void Init()
        {
            if (_initialized)
                return;

            _mainMenuState = new MainMenuState(_sceneModule, _uiModule, EnterDemo);
            _stateMachine.AddState(_mainMenuState);
            _stateMachine.AddState(new DemoState(_sceneModule, _unitSystem, ReturnToMainMenu));
            _initialized = true;
        }

        public void Start()
        {
            if (!_initialized || _started)
                return;

            _monoModule.OnUpdate += OnUpdate;
            _started = true;

            if (!_stateMachine.ChangeState<MainMenuState>())
                Debug.LogError("GameFlow进入MainMenu状态失败");
        }

        public void Stop()
        {
            if (!_started)
                return;

            _monoModule.OnUpdate -= OnUpdate;
            _stateMachine.Stop();
            _started = false;
        }

        public void Dispose()
        {
            Stop();
            _stateMachine.Clear();
            _mainMenuState = null;
            _initialized = false;
        }

        /// <summary>请求从已就绪的主菜单进入 Demo；返回值只表示请求是否被接受。</summary>
        public bool EnterDemo()
        {
            if (!_started || !_stateMachine.IsCurrent<MainMenuState>() || !_mainMenuState.IsReady)
                return false;

            return _stateMachine.ChangeState<DemoState>();
        }

        private void ReturnToMainMenu()
        {
            if (_started && _stateMachine.IsCurrent<DemoState>())
                _stateMachine.ChangeState<MainMenuState>();
        }

        private void OnUpdate(float dt)
        {
            _stateMachine.Update(dt);
        }
    }
}
