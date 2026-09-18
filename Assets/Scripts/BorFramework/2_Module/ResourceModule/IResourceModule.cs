using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BorFramework
{
    public interface IResourceModule : IModule
    {
        string PackageName { get; }
        EResourceState State { get; }
        UniTask<IAssetLease<T>> LoadAssetAsync<T>(string address) where T : Object;
        IAssetLease<T> LoadAsset<T>(string address) where T : Object;

        UniTask<IReadOnlyList<string>> GetAssetAddressesAsync(string tag);

        /// <summary>
        /// 实例化预制体并持有它依赖的资源引用。调用方不再使用实例时必须释放返回值。
        /// </summary>
        UniTask<IInstanceLease> InstantiateAsync(
            string address,
            Vector3 position,
            Quaternion rotation,
            Transform parent);
    }
}
