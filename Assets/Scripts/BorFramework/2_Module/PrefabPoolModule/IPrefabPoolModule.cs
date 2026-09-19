using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BorFramework
{
    /// <summary>
    /// 按资源地址复用 Prefab 实例，并在池桶存活期间持有对应资源租约。
    /// </summary>
    public interface IPrefabPoolModule : IModule
    {
        /// <summary>
        /// 租用一个尚未激活的 Prefab 实例。调用方完成未激活状态下安全的初始化后自行激活实例。
        /// </summary>
        UniTask<GameObject> RentAsync(
            string address,
            Vector3 position,
            Quaternion rotation,
            Transform parent);

        /// <summary>
        /// 将实例归还原池桶。不是本模块租出的实例返回 false。
        /// </summary>
        bool Return(GameObject instance);
    }
}
