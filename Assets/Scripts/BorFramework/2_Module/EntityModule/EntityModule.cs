using System;
using System.Collections.Generic;

namespace BorFramework
{
    public sealed class EntityModule : IEntityModule
    {
        private readonly IMonoModule _monoModule;
        private readonly List<Entity> _entities = new();
        private readonly List<Entity> _pendingAdditions = new();
        private readonly Queue<PendingRemoval> _pendingRemovals = new();
        private bool _started;
        private bool _isUpdating;
        private bool _disposed;

        public EntityModule(IMonoModule monoModule)
        {
            _monoModule = monoModule;
        }
        
        public void Init()
        {
        }

        public void Start()
        {
            if (_started || _disposed)
                return;

            _started = true;
            foreach (Entity entity in _entities)
                entity.Start();

            _monoModule.OnUpdate += OnUpdate;
        }

        public void Stop()
        {
            if (!_started)
                return;

            _started = false;
            _monoModule.OnUpdate -= OnUpdate;

            bool wasUpdating = _isUpdating;
            _isUpdating = true;
            for (int i = _entities.Count - 1; i >= 0; i--)
                _entities[i].Stop();

            if (!wasUpdating)
                CompleteUpdate();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();

            for (int i = _pendingAdditions.Count - 1; i >= 0; i--)
                QueueRemoval(_pendingAdditions[i], null);

            for (int i = _entities.Count - 1; i >= 0; i--)
                QueueRemoval(_entities[i], null);

            if (!_isUpdating)
                FlushRemovals();
        }

        public T AddEntity<T>(T entity) where T : Entity
        {
            if (_disposed
                || entity == null
                || entity.IsDisposed
                || entity.IsRemovalPending
                || _entities.Contains(entity)
                || _pendingAdditions.Contains(entity))
            {
                return null;
            }

            if (_isUpdating)
            {
                _pendingAdditions.Add(entity);
                return entity;
            }

            _entities.Add(entity);
            if (_started)
                entity.Start();

            return entity;
        }

        public bool RemoveEntity(Entity entity, Action onRemoved = null)
        {
            if (entity == null
                || entity.IsRemovalPending
                || (!_entities.Contains(entity) && !_pendingAdditions.Contains(entity)))
            {
                return false;
            }

            QueueRemoval(entity, onRemoved);
            if (!_isUpdating)
                FlushRemovals();

            return true;
        }

        private void OnUpdate(float dt)
        {
            if (!_started)
                return;

            _isUpdating = true;
            for (int i = 0; i < _entities.Count && _started; i++)
                _entities[i].Tick(dt);

            CompleteUpdate();
        }

        private void QueueRemoval(Entity entity, Action onRemoved)
        {
            if (entity.IsRemovalPending)
                return;

            entity.RequestRemoval();
            _pendingRemovals.Enqueue(new PendingRemoval(entity, onRemoved));
        }

        private void CompleteUpdate()
        {
            FlushRemovals();
            _isUpdating = false;

            foreach (Entity entity in _pendingAdditions)
            {
                _entities.Add(entity);
                if (_started)
                    entity.Start();
            }

            _pendingAdditions.Clear();
        }

        private void FlushRemovals()
        {
            while (_pendingRemovals.Count > 0)
            {
                PendingRemoval removal = _pendingRemovals.Dequeue();
                _entities.Remove(removal.Entity);
                _pendingAdditions.Remove(removal.Entity);
                removal.Entity.Dispose();
                removal.OnRemoved?.Invoke();
            }
        }

        private readonly struct PendingRemoval
        {
            public Entity Entity { get; }
            public Action OnRemoved { get; }

            public PendingRemoval(Entity entity, Action onRemoved)
            {
                Entity = entity;
                OnRemoved = onRemoved;
            }
        }
    }
}
