namespace BorFramework
{
    public abstract class UIViewModelBase
    {
        public virtual void OnOpen() { }
        public virtual void OnPause() { }
        public virtual void OnResume() { }
        public virtual void OnClose() { }
        public virtual void Dispose() { }
    }
}
