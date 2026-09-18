using System;
using System.Collections.Generic;

namespace BorFramework
{
    public abstract class UIView<TViewModel> : UIViewBase
        where TViewModel : UIViewModelBase
    {
        private readonly List<Action> _unbindActions = new();

        protected TViewModel ViewModel { get; private set; }

        public override bool BindViewModel(UIViewModelBase viewModel)
        {
            if (viewModel is not TViewModel typedViewModel)
                return false;

            ViewModel = typedViewModel;
            OnBind();
            return true;
        }

        public override void UnbindViewModel()
        {
            OnUnbind();

            for (var i = _unbindActions.Count - 1; i >= 0; i--)
                _unbindActions[i]();

            _unbindActions.Clear();
            ViewModel = null;
        }

        protected void Bind<T>(BindableProperty<T> property, Action<T> listener)
        {
            if (property == null || listener == null)
                return;

            property.Subscribe(listener);
            _unbindActions.Add(() => property.Unsubscribe(listener));
        }

        protected abstract void OnBind();

        protected virtual void OnUnbind() { }
    }
}
