using System;
using Cysharp.Threading.Tasks;

namespace BorFramework
{
    public interface IUIModule : IModule
    {
        void Register<TView, TViewModel>(
            string address,
            EUILayer layer,
            Func<TViewModel> viewModelFactory,
            EUISubLayer subLayer = EUISubLayer.Layer1)
            where TView : UIView<TViewModel>
            where TViewModel : UIViewModelBase;

        /// <summary>
        /// 打开已注册界面。Screen 和 Window 分别遵循页面、窗口的入栈规则；其他层直接显示。
        /// 加载失败、请求被关闭或被更新的同类型请求替代时返回 null。
        /// </summary>
        UniTask<TView> OpenAsync<TView>() where TView : UIViewBase;

        /// <summary>
        /// 加载成功后隐藏当前页面并将新页面入栈。栈中已存在同类型页面时拒绝并返回 null；
        /// 等待期间发生后续导航、关闭或停止时返回 null，不显示过期界面。
        /// </summary>
        UniTask<TView> PushScreenAsync<TView>() where TView : UIViewBase;

        /// <summary>
        /// 在当前页面上打开窗口。等待期间页面或导航版本变化、窗口被关闭时返回 null；
        /// 栈中已存在同类型窗口时拒绝并返回 null。
        /// </summary>
        UniTask<TView> OpenWindowAsync<TView>() where TView : UIViewBase;

        void Close<TView>() where TView : UIViewBase;

        void Destroy<TView>() where TView : UIViewBase;

        bool TryGet<TView>(out TView view) where TView : UIViewBase;

        /// <summary>是否处于打开生命周期；被后续页面遮盖并隐藏的页面仍为打开状态。</summary>
        bool IsOpen<TView>() where TView : UIViewBase;

        /// <summary>关闭当前页面及其窗口，并重新显示上一个页面。</summary>
        bool PopScreen();

        void Back();
    }
}
