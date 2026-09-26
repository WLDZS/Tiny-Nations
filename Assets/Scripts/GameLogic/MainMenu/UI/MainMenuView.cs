using BorFramework;
using UnityEngine;

namespace GameLogic.MainMenu.UI
{
    public sealed class MainMenuView : UIView<MainMenuViewModel>
    {
        [SerializeField]
        private UnityEngine.UI.Button _demoButton;

        [SerializeField]
        private UnityEngine.UI.Button _navigationTestButton;

        protected override void OnBind()
        {
            if (_demoButton == null || _navigationTestButton == null)
            {
                Debug.LogError("MainMenuView缺少场景入口按钮引用");
                return;
            }

            _demoButton.interactable = true;
            _navigationTestButton.interactable = true;
            _demoButton.onClick.AddListener(OnDemoButtonClicked);
            _navigationTestButton.onClick.AddListener(OnNavigationTestButtonClicked);
        }

        protected override void OnUnbind()
        {
            if (_demoButton != null)
                _demoButton.onClick.RemoveListener(OnDemoButtonClicked);

            if (_navigationTestButton != null)
                _navigationTestButton.onClick.RemoveListener(OnNavigationTestButtonClicked);
        }

        private void OnDemoButtonClicked()
        {
            if (_demoButton == null)
                return;

            _demoButton.interactable = false;
            _navigationTestButton.interactable = false;
            if (ViewModel.EnterDemo())
                return;

            _demoButton.interactable = true;
            _navigationTestButton.interactable = true;
            Debug.LogWarning("MainMenu无法切换到Demo状态");
        }

        private void OnNavigationTestButtonClicked()
        {
            if (_demoButton == null || _navigationTestButton == null)
                return;

            _demoButton.interactable = false;
            _navigationTestButton.interactable = false;
            if (ViewModel.EnterNavigationTest())
                return;

            _demoButton.interactable = true;
            _navigationTestButton.interactable = true;
            Debug.LogWarning("MainMenu无法切换到导航测试状态");
        }
    }
}
