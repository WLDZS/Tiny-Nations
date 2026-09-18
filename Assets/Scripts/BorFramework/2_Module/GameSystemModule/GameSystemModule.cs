using System;
using System.Collections.Generic;

namespace BorFramework
{
    public sealed class GameSystemModule : IGameSystemModule
    {
        private readonly Dictionary<Type, IGameSystem> _systems = new();
        private readonly List<IGameSystem> _systemOrder = new();
        private bool _initialized;
        private bool _started;

        public void AddSystem<T>(T system) where T : class, IGameSystem
        {
            if (system == null)
                return;

            if (!_systems.TryAdd(typeof(T), system))
                return;

            _systemOrder.Add(system);

            if (_initialized)
                system.Init();

            if (_started)
                system.Start();
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            _systems.TryGetValue(typeof(T), out var system);
            return system as T;
        }

        public void Init()
        {
            if (_initialized)
                return;

            foreach (var system in _systemOrder)
                system.Init();

            _initialized = true;
        }

        public void Start()
        {
            if (_started)
                return;

            foreach (var system in _systemOrder)
                system.Start();

            _started = true;
        }

        public void Stop()
        {
            if (!_started)
                return;

            for (int i = _systemOrder.Count - 1; i >= 0; i--)
                _systemOrder[i].Stop();

            _started = false;
        }

        public void Dispose()
        {
            Stop();

            for (int i = _systemOrder.Count - 1; i >= 0; i--)
                _systemOrder[i].Dispose();

            _systems.Clear();
            _systemOrder.Clear();
            _initialized = false;
        }
    }
}
