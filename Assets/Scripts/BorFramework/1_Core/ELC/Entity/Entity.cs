using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorFramework
{
    public abstract class Entity
    {
        protected GameObject Go;
        private readonly HashSet<Type> _logicTypes = new();
        private readonly List<ILogic> _logicOrder = new();
        private bool _isRunning;
        private bool _hasStarted;

        public bool IsDisposed { get; private set; }

        internal bool IsRemovalPending { get; private set; }

        internal void Start()
        {
            if (IsDisposed || IsRemovalPending)
                return;

            _hasStarted = true;
            _isRunning = true;
        }

        internal void Tick(float dt)
        {
            for (int i = 0; i < _logicOrder.Count; i++)
            {
                if (!_isRunning || IsDisposed || IsRemovalPending)
                    return;

                _logicOrder[i].OnUpdate(dt);
            }
        }

        internal void Stop()
        {
            _isRunning = false;
            for (int i = _logicOrder.Count - 1; i >= 0; i--)
                _logicOrder[i].Stop();
        }

        internal void RequestRemoval()
        {
            IsRemovalPending = true;
        }

        /// <summary>停止并释放全部行为；已释放实体不能重新加入调度。</summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            Stop();

            for (int i = _logicOrder.Count - 1; i >= 0; i--)
                _logicOrder[i].Dispose();

            _logicOrder.Clear();
            _logicTypes.Clear();
            Go = null;
        }

        /// <summary>仅在首次启动前组装行为；相同类型拒绝重复注册，同阶段保留注册顺序。</summary>
        protected bool AddLogic<T>(T logic) where T : ILogic
        {
            if (logic == null || IsDisposed || _hasStarted || !_logicTypes.Add(logic.GetType()))
                return false;

            int index = _logicOrder.Count;
            while (index > 0 && _logicOrder[index - 1].Phase > logic.Phase)
                index--;

            _logicOrder.Insert(index, logic);
            return true;
        }
    }
}
