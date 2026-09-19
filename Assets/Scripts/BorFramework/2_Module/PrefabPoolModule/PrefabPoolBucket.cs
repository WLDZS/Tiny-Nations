using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace BorFramework
{
    internal sealed class PrefabPoolBucket
    {
        private readonly string _address;
        private readonly Transform _poolRoot;
        private readonly IAssetLease<GameObject> _assetLease;
        private readonly ObjectPool<GameObject> _pool;
        private readonly HashSet<GameObject> _rentedInstances = new();
        private readonly Vector3 _prefabLocalScale;
        private bool _disposed;

        public PrefabPoolBucket(
            string address,
            Transform poolRoot,
            IAssetLease<GameObject> assetLease,
            int maxInactive)
        {
            _address = address;
            _poolRoot = poolRoot;
            _assetLease = assetLease;
            _prefabLocalScale = assetLease.Asset.transform.localScale;

            _pool = new ObjectPool<GameObject>(
                CreateInstance,
                OnGet,
                OnRelease,
                DestroyInstance,
                false,
                Mathf.Min(4, maxInactive),
                maxInactive);
        }

        public GameObject Rent(
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            if (_disposed)
                return null;

            GameObject instance = _pool.Get();
            if (instance == null)
                return null;

            Transform instanceTransform = instance.transform;
            MoveToActiveScene(instance, parent);
            instanceTransform.SetParent(parent, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = _prefabLocalScale;
            _rentedInstances.Add(instance);
            return instance;
        }

        public bool Return(GameObject instance)
        {
            if (_disposed || ReferenceEquals(instance, null))
                return false;

            if (!_rentedInstances.Remove(instance))
            {
                Debug.LogWarning($"Prefab实例未由当前池桶租出，无法归还。Address: {_address}", instance);
                return false;
            }

            if (instance == null)
                return true;

            _pool.Release(instance);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (GameObject instance in _rentedInstances)
            {
                if (instance != null)
                    Object.Destroy(instance);
            }

            _rentedInstances.Clear();
            _pool.Clear();
            _assetLease.Dispose();
        }

        private GameObject CreateInstance()
        {
            if (_disposed || !_assetLease.IsValid || _assetLease.Asset == null)
                return null;

            GameObject instance = Object.Instantiate(_assetLease.Asset, _poolRoot);
            instance.name = _assetLease.Asset.name;
            instance.SetActive(false);
            return instance;
        }

        private static void OnGet(GameObject instance)
        {
            if (instance != null)
                instance.SetActive(false);
        }

        private void OnRelease(GameObject instance)
        {
            if (instance == null)
                return;

            instance.SetActive(false);
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(null, false);
            Object.DontDestroyOnLoad(instance);
            instanceTransform.SetParent(_poolRoot, false);
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = _prefabLocalScale;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance != null)
                Object.Destroy(instance);
        }

        private static void MoveToActiveScene(
            GameObject instance,
            Transform parent)
        {
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(null, false);

            Scene targetScene = parent != null
                ? parent.gameObject.scene
                : SceneManager.GetActiveScene();
            if (targetScene.IsValid()
                && targetScene.isLoaded
                && instance.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(instance, targetScene);
            }
        }
    }
}
