using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace BorFramework
{
    public class ResourceModule : IResourceModule
    {
        // Keep the editor resource configuration reloadable after YooAsset settings are imported.
        private const string DefaultPackageName = "DefaultPackage";

        private readonly string _packageName;
        private ResourcePackage _package;
        public string PackageName => _packageName;
        public EResourceState State { get; private set; } = EResourceState.None;

        public ResourceModule(string packageName = DefaultPackageName)
        {
            _packageName = packageName;
        }

        public void Init()
        {
            if (State != EResourceState.None)
                return;

            State = EResourceState.Initializing;
            YooAssets.Initialize(new YooAssetLogger());
            _package = YooAssets.CreatePackage(_packageName);
            InitializePackageAsync().Forget();
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            if (State == EResourceState.Disposed)
                return;

            State = EResourceState.Disposed;

            if (!YooAssets.IsInitialized)
                return;

            // 整体退出时由 YooAsset 统一销毁，场景异步操作不能同步等待。
            YooAssets.Destroy();
            _package = null;
        }

        public async UniTask<IAssetLease<T>> LoadAssetAsync<T>(string address)
            where T : Object
        {
            if (State == EResourceState.None || State == EResourceState.Disposed)
            {
                Debug.LogWarning("ResourceModule尚未初始化或已经释放");
                return null;
            }

            if (State == EResourceState.Initializing)
                await UniTask.WaitUntil(() => State != EResourceState.Initializing);

            if (State != EResourceState.Ready || _package == null)
                return null;

            var handle = _package.LoadAssetAsync<T>(address);
            await handle;
            return CreateLease<T>(address, handle);
        }

        public IAssetLease<T> LoadAsset<T>(string address)
            where T : Object
        {
            if (State != EResourceState.Ready || _package == null)
            {
                Debug.LogWarning("ResourceModule尚未准备完成，同步加载不可用，请使用 LoadAssetAsync");
                return null;
            }

            var handle = _package.LoadAssetSync<T>(address);
            return CreateLease<T>(address, handle);
        }

        public async UniTask<IReadOnlyList<string>> GetAssetAddressesAsync(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)
                || State == EResourceState.None
                || State == EResourceState.Disposed)
            {
                return System.Array.Empty<string>();
            }

            if (State == EResourceState.Initializing)
                await UniTask.WaitUntil(() => State != EResourceState.Initializing);

            if (State != EResourceState.Ready || _package == null)
                return System.Array.Empty<string>();

            AssetInfo[] assetInfos = _package.GetAssetInfos(tag);
            var addresses = new List<string>(assetInfos.Length);

            for (int i = 0; i < assetInfos.Length; i++)
            {
                string address = assetInfos[i].Address;
                if (!string.IsNullOrWhiteSpace(address))
                    addresses.Add(address);
            }

            addresses.Sort(System.StringComparer.Ordinal);
            return addresses;
        }

        public async UniTask<IInstanceLease> InstantiateAsync(
            string address,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            IAssetLease<GameObject> assetLease = await LoadAssetAsync<GameObject>(address);
            if (assetLease == null)
                return null;

            GameObject instance = Object.Instantiate(assetLease.Asset, position, rotation, parent);
            if (instance != null)
                return new InstanceLease(address, instance, assetLease);

            assetLease.Dispose();
            Debug.LogError($"预制体实例化失败。Address: {address}");
            return null;
        }

        private IAssetLease<T> CreateLease<T>(string address, AssetHandle handle)
            where T : Object
        {
            if (handle == null || handle.Status != EOperationStatus.Succeeded)
            {
                var error = handle == null ? "Handle为空" : handle.Error;
                handle?.Release();
                Debug.LogError($"资源加载失败。Package: {_packageName}, Address: {address}, Error: {error}");
                return null;
            }

            var asset = handle.GetAssetObject<T>();
            if (asset == null)
            {
                handle.Release();
                Debug.LogError(
                    $"资源类型不匹配或对象为空。Package: {_packageName}, Address: {address}, Type: {typeof(T).FullName}");
                return null;
            }

            return new AssetLease<T>(address, handle, asset);
        }

        private async UniTask InitializePackageAsync()
        {
#if UNITY_EDITOR
            var buildResult = EditorSimulateBuildInvoker.Build(_packageName, (int)EBundleType.VirtualAssetBundle);
            var options = new EditorSimulateModeOptions
            {
                EditorFileSystemParameters =
                    FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory)
            };
#else
            var options = new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
            };
#endif
            var initializeOperation = _package.InitializePackageAsync(options);
            await initializeOperation;
            if (State != EResourceState.Initializing)
                return;

            if (initializeOperation.Status != EOperationStatus.Succeeded)
            {
                MarkFailed($"资源包初始化失败: {initializeOperation.Error}");
                return;
            }
            
            var versionOperation = _package.RequestPackageVersionAsync();
            await versionOperation;
            if (State != EResourceState.Initializing)
                return;

            if (versionOperation.Status != EOperationStatus.Succeeded)
            {
                MarkFailed($"Request package version failed: {versionOperation.Error}");
                return;
            }

            var manifestOperation = _package.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOperation.PackageVersion, 60));
            await manifestOperation;
            if (State != EResourceState.Initializing)
                return;

            if (manifestOperation.Status != EOperationStatus.Succeeded)
            {
                MarkFailed($"Load package manifest failed: {manifestOperation.Error}");
                return;
            }

            State = EResourceState.Ready;
        }

        private void MarkFailed(string message)
        {
            State = EResourceState.Failed;
            Debug.LogError(message);
        }
    }
}
