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

        UniTask<TView> OpenAsync<TView>() where TView : UIViewBase;

        UniTask<TView> PushScreenAsync<TView>() where TView : UIViewBase;

        UniTask<TView> OpenWindowAsync<TView>() where TView : UIViewBase;

        void Close<TView>() where TView : UIViewBase;

        void Destroy<TView>() where TView : UIViewBase;

        bool TryGet<TView>(out TView view) where TView : UIViewBase;

        bool IsOpen<TView>() where TView : UIViewBase;

        bool PopScreen();

        void Back();
    }
}
