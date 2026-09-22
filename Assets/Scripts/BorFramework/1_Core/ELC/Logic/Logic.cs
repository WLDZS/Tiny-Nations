namespace BorFramework
{
    public abstract class Logic : ILogic
    {
        private int _blockCount;
        private bool _isRunning;
        private bool _disposed;

        public virtual ELogicPhase Phase => ELogicPhase.Command;

        public bool IsBlocked => _blockCount > 0;

        /// <summary>首次更新时启动；阻塞或释放后不执行当前帧行为。</summary>
        public void OnUpdate(float dt)
        {
            if (_disposed)
                return;

            if (IsBlocked)
            {
                Stop();
                return;
            }

            if (!_isRunning)
            {
                _isRunning = true;
                OnStart();
            }

            if (_isRunning && !_disposed && !IsBlocked)
                OnTick(dt);
        }

        /// <summary>停止已启动的行为；之后允许通过更新重新启动。</summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            OnStop();
        }

        /// <summary>先停止行为，再永久释放其持有状态；重复调用无效。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
            OnDispose();
            _blockCount = 0;
        }

        public void Block()
        {
            if (_disposed)
                return;

            _blockCount++;
        }

        public void UnBlock()
        {
            if (_blockCount > 0)
                _blockCount--;
        }

        protected virtual void OnStart() { }

        protected abstract void OnTick(float dt);

        protected virtual void OnStop() { }

        protected virtual void OnDispose() { }
    }
}
