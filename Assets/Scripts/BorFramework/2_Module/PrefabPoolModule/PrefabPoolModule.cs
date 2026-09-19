using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BorFramework
{
    public sealed class PrefabPoolModule : IPrefabPoolModule
    {
        private const int DefaultMaxInactive = 32;

        private readonly IResourceModule _resourceModule;
        private readonly Dictionary<string, PrefabPoolBucket> _buckets =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTask<PrefabPoolBucket>> _loadingTasks =
            new(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, PrefabPoolBucket> _rentedInstances = new();
        private Transform _poolRoot;
        private bool _initialized;
        private bool _running;
        private int _lifecycleVersion;

        public PrefabPoolModule(IResourceModule resourceModule)
        {
            _resourceModule = resourceModule;
        }

        public void Init()
        {
            if (_initialized)
                return;

            var root = new GameObject("[PrefabPool]");
            root.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(root);
            _poolRoot = root.transform;
            _initialized = true;
        }

        public void Start()
        {
            if (!_initialized || _running)
                return;

            _running = true;
            _lifecycleVersion++;
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _lifecycleVersion++;
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            _running = false;
            _initialized = false;
            _lifecycleVersion++;
            _loadingTasks.Clear();
            _rentedInstances.Clear();

            foreach (PrefabPoolBucket bucket in _buckets.Values)
                bucket.Dispose();

            _buckets.Clear();

            if (_poolRoot != null)
                UnityEngine.Object.Destroy(_poolRoot.gameObject);

            _poolRoot = null;
        }

        public async UniTask<GameObject> RentAsync(
            string address,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            if (!_running || string.IsNullOrWhiteSpace(address))
                return null;

            PrefabPoolBucket bucket = await GetOrCreateBucketAsync(address);
            if (!_running || bucket == null)
                return null;

            GameObject instance = bucket.Rent(position, rotation, parent);
            if (instance == null)
                return null;

            _rentedInstances.Add(instance, bucket);
            return instance;
        }

        public bool Return(GameObject instance)
        {
            if (ReferenceEquals(instance, null))
                return false;

            if (!_rentedInstances.Remove(instance, out PrefabPoolBucket bucket))
            {
                Debug.LogWarning("Prefab实例不是由当前对象池租出的，无法归还。", instance);
                return false;
            }

            return bucket.Return(instance);
        }

        private async UniTask<PrefabPoolBucket> GetOrCreateBucketAsync(string address)
        {
            if (_buckets.TryGetValue(address, out PrefabPoolBucket bucket))
                return bucket;

            if (_loadingTasks.TryGetValue(address, out UniTask<PrefabPoolBucket> loadingTask))
                return await loadingTask;

            int lifecycleVersion = _lifecycleVersion;
            loadingTask = CreateBucketAsync(address, lifecycleVersion).Preserve();
            _loadingTasks.Add(address, loadingTask);
            bucket = await loadingTask;
            _loadingTasks.Remove(address);
            return bucket;
        }

        private async UniTask<PrefabPoolBucket> CreateBucketAsync(
            string address,
            int lifecycleVersion)
        {
            if (_resourceModule == null)
                return null;

            IAssetLease<GameObject> assetLease =
                await _resourceModule.LoadAssetAsync<GameObject>(address);

            if (!IsCurrent(lifecycleVersion))
            {
                assetLease?.Dispose();
                return null;
            }

            if (assetLease == null || !assetLease.IsValid || assetLease.Asset == null)
            {
                assetLease?.Dispose();
                return null;
            }

            var bucket = new PrefabPoolBucket(
                address,
                _poolRoot,
                assetLease,
                DefaultMaxInactive);
            _buckets.Add(address, bucket);
            return bucket;
        }

        private bool IsCurrent(int lifecycleVersion)
        {
            return _running
                   && lifecycleVersion == _lifecycleVersion
                   && _poolRoot != null;
        }
    }
}
