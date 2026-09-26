using System;
using BorFramework;
using GameLogic.Economy;
using GameLogic.Navigation;
using GameLogic.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.GameFlow
{
    /// <summary>Owns the scene's resource, navigation, and unit systems.</summary>
    public sealed class UnitSceneSystems : IDisposable
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

        public IPlayerResourceSystem PlayerResources { get; private set; }

        public INavigationSystem Navigation { get; private set; }

        public UnitSceneSystems(
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

        /// <summary>Builds and registers navigation before the scene's unit system.</summary>
        public bool Start(Scene scene)
        {
            if (_started || _systemModule == null)
                return false;

            var playerResources = new PlayerResourceSystem();
            if (!_systemModule.AddSystem<IPlayerResourceSystem>(playerResources))
                return false;

            if (!NavigationSystem.TryCreate(scene, out NavigationSystem navigationSystem))
            {
                Debug.LogError($"导航地图构建失败：场景 {scene.name} 的 Ground、Collision 或可行走格子无效。");
                _systemModule.RemoveSystem<IPlayerResourceSystem>();
                return false;
            }

            if (!_systemModule.AddSystem<INavigationSystem>(navigationSystem))
            {
                _systemModule.RemoveSystem<IPlayerResourceSystem>();
                return false;
            }

            var unitSystem = new UnitSystem(
                _resourceModule,
                _prefabPoolModule,
                _entityModule,
                _inputModule,
                _eventModule,
                _monoModule,
                navigationSystem);

            if (!_systemModule.AddSystem<IUnitSystem>(unitSystem))
            {
                _systemModule.RemoveSystem<INavigationSystem>();
                _systemModule.RemoveSystem<IPlayerResourceSystem>();
                return false;
            }

            PlayerResources = playerResources;
            Navigation = navigationSystem;
            UnitSystem = unitSystem;
            _started = true;
            return true;
        }

        /// <summary>Removes the scene systems when the owning state exits.</summary>
        public void Dispose()
        {
            if (!_started)
                return;

            _started = false;
            UnitSystem = null;
            Navigation = null;
            PlayerResources = null;
            _systemModule.RemoveSystem<IUnitSystem>();
            _systemModule.RemoveSystem<INavigationSystem>();
            _systemModule.RemoveSystem<IPlayerResourceSystem>();
        }
    }
}
