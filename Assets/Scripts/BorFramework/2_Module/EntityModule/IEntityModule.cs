using System;

namespace BorFramework
{
    public interface IEntityModule : IModule
    {
        /// <summary>
        /// 接收实体所有权并加入调度。空、重复、待移除或已释放实体返回 null，所有权不变。
        /// 更新中加入的实体从下一帧开始更新。
        /// </summary>
        T AddEntity<T>(T entity) where T : Entity;

        /// <summary>
        /// 移除并释放所属实体。更新中请求会立即停止后续行为，在本批更新结束后释放。
        /// 成功释放后调用 onRemoved；宿主实例与资源应在此回调中回收。无所属实体返回 false。
        /// </summary>
        bool RemoveEntity(Entity entity, Action onRemoved = null);
    }
}
