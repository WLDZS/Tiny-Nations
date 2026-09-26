namespace BorFramework
{
    public interface IGameSystemModule : IModule
    {
        /// <summary>
        /// 成功时接收系统所有权，并按模块当前阶段初始化、启动。
        /// 空、重复类型、重复实例或已释放模块返回 false，不改变既有所有权。
        /// 场景系统可动态注册；模块 Stop 时允许系统退出流程移除场景系统。
        /// </summary>
        bool AddSystem<T>(T system) where T : class, IGameSystem;

        /// <summary>按注册类型查找系统；未注册时返回 null。</summary>
        T GetSystem<T>() where T : class, IGameSystem;

        /// <summary>移除指定注册类型，先停止再释放；未注册时返回 false。</summary>
        bool RemoveSystem<T>() where T : class, IGameSystem;
    }
}
