using BorFramework;
using UnityEngine;

namespace GameLogic.MainMenu.UI
{
    public sealed class MainMenuView : UIView<MainMenuViewModel>
    {
        [SerializeField]
        private UnityEngine.UI.Button _demoButton;

        protected override void OnBind()
        {
            if (_demoButton == null)
            {
                Debug.LogError("MainMenuView缺少Demo按钮引用");
                return;
            }

            _demoButton.onClick.AddListener(OnDemoButtonClicked);
        }

        protected override void OnUnbind()
        {
            if (_demoButton != null)
                _demoButton.onClick.RemoveListener(OnDemoButtonClicked);
        }

        private void OnDemoButtonClicked()
        {
            if (_demoButton == null)
                return;

            _demoButton.interactable = false;
            if (ViewModel.EnterDemo())
                return;

            _demoButton.interactable = true;
            Debug.LogWarning("MainMenu无法切换到Demo状态");
        }
    }
}
