using UnityEngine;

namespace BorFramework
{
    internal sealed class InstanceLease : IInstanceLease
    {
        private IAssetLease<GameObject> _assetLease;

        public GameObject Instance { get; private set; }
        public string Address { get; }
        public bool IsValid => Instance != null && _assetLease != null && _assetLease.IsValid;

        public InstanceLease(
            string address,
            GameObject instance,
            IAssetLease<GameObject> assetLease)
        {
            Address = address;
            Instance = instance;
            _assetLease = assetLease;
        }

        public void Dispose()
        {
            if (Instance != null)
            {
                Object.Destroy(Instance);
                Instance = null;
            }

            _assetLease?.Dispose();
            _assetLease = null;
        }
    }
}
