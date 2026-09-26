using System;
using BorFramework;
using GameLogic.Navigation;
using GameLogic.Units;

namespace GameLogic.GameFlow
{
    /// <summary>Owns the systems needed while a navigation gameplay scene is active.</summary>
    public sealed class NavigationSceneSystems : IDisposable
    {
        private readonly IGameSystemModule _systemModule;
        private readonly IResourceModule _resourceModule;
        private readonly IPrefabPoolModule _prefabPoolModule;
        private readonly IEntityModule _entityModule;
        private readonly IInputModule _inputModule;
        private readonly IEventModule _eventModule;
        private readonly IMonoModule _monoModule;
        private bool _started;

        public IUnitSystem UnitSystem { get; private set; }

        public NavigationSceneSystems(
            IGameSystemModule systemModule,
            IResourceModule resourceModule,
            IPrefabPoolModule prefabPoolModule,
            IEntityModule entityModule,
            IInputModule inputModule,
            IEventModule eventModule,
            IMonoModule monoModule)
        {
            _systemModule = systemModule;
            _resourceModule = resourceModule;
            _prefabPoolModule = prefabPoolModule;
            _entityModule = entityModule;
            _inputModule = inputModule;
            _eventModule = eventModule;
            _monoModule = monoModule;
        }

        /// <summary>Registers navigation before units; returns false without retaining partial registration.</summary>
        public bool Start()
        {
            if (_started || _systemModule == null)
                return false;

            var navigationSystem = new NavigationSystem();
            var unitSystem = new UnitSystem(
                _resourceModule,
                _prefabPoolModule,
                _entityModule,
                _inputModule,
                _eventModule,
                _monoModule,
                navigationSystem);

            if (!_systemModule.AddSystem<INavigationSystem>(navigationSystem))
                return false;

            if (!_systemModule.AddSystem<IUnitSystem>(unitSystem))
            {
                _systemModule.RemoveSystem<INavigationSystem>();
                return false;
            }

            UnitSystem = unitSystem;
            _started = true;
            return true;
        }

        /// <summary>Removes units before navigation when the owning scene state exits.</summary>
        public void Dispose()
        {
            if (!_started)
                return;

            _started = false;
            UnitSystem = null;
            _systemModule.RemoveSystem<IUnitSystem>();
            _systemModule.RemoveSystem<INavigationSystem>();
        }
    }
}
