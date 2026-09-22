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
        private bool _disposed;

        public bool AddSystem<T>(T system) where T : class, IGameSystem
        {
            if (_disposed || system == null || _systemOrder.Contains(system))
                return false;

            if (!_systems.TryAdd(typeof(T), system))
                return false;

            _systemOrder.Add(system);

            if (_initialized)
                system.Init();

            if (_started)
                system.Start();

            return true;
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            _systems.TryGetValue(typeof(T), out var system);
            return system as T;
        }

        public bool RemoveSystem<T>() where T : class, IGameSystem
        {
            if (!_systems.Remove(typeof(T), out IGameSystem system))
                return false;

            _systemOrder.Remove(system);
            system.Stop();
            system.Dispose();
            return true;
        }

        public void Init()
        {
            if (_initialized || _disposed)
                return;

            foreach (var system in _systemOrder)
                system.Init();

            _initialized = true;
        }

        public void Start()
        {
            if (!_initialized || _started || _disposed)
                return;

            foreach (var system in _systemOrder)
                system.Start();

            _started = true;
        }

        public void Stop()
        {
            if (!_started)
                return;

            _started = false;
            for (int i = _systemOrder.Count - 1; i >= 0; i--)
                _systemOrder[i].Stop();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();

            for (int i = _systemOrder.Count - 1; i >= 0; i--)
                _systemOrder[i].Dispose();

            _systems.Clear();
            _systemOrder.Clear();
            _initialized = false;
        }
    }
}
