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

            _stateMachine.AddState(new MainMenuState(_sceneModule, _uiModule, EnterDemo));
            _stateMachine.AddState(new DemoState(_sceneModule, _unitSystem));
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
            _initialized = false;
        }

        public bool EnterDemo()
        {
            if (!_started || !_stateMachine.IsCurrent<MainMenuState>())
                return false;

            return _stateMachine.ChangeState<DemoState>();
        }

        private void OnUpdate(float dt)
        {
            _stateMachine.Update(dt);
        }
    }
}
