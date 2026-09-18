using System;
using UnityEngine;

namespace BorFramework
{
    /// <summary>
    /// 表示一个由资源模块创建的实例。释放时会销毁实例并释放资源引用。
    /// </summary>
    public interface IInstanceLease : IDisposable
    {
        GameObject Instance { get; }
        string Address { get; }
        bool IsValid { get; }
    }
}
