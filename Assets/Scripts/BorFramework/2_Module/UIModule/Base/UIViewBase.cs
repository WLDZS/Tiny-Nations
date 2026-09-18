using UnityEngine;

namespace BorFramework
{
    public abstract class UIViewBase : MonoBehaviour
    {
        public abstract bool BindViewModel(UIViewModelBase viewModel);
        public abstract void UnbindViewModel();
    }
}
