namespace BorFramework
{
    public interface IGameSystemModule : IModule
    {
        void AddSystem<T>(T system) where T : class, IGameSystem;
        T GetSystem<T>() where T : class, IGameSystem;
    }
}
