using System;
using BorFramework;

namespace GameLogic.MainMenu.UI
{
    public sealed class MainMenuViewModel : UIViewModelBase
    {
        private readonly Func<bool> _enterDemo;

        private readonly Func<bool> _enterNavigationTest;

        public MainMenuViewModel(Func<bool> enterDemo, Func<bool> enterNavigationTest)
        {
            _enterDemo = enterDemo;
            _enterNavigationTest = enterNavigationTest;
        }

        public override void OnOpen()
        {
        }

        public override void OnClose()
        {
        }

        public override void Dispose()
        {
        }

        public bool EnterDemo()
        {
            return _enterDemo != null && _enterDemo();
        }

        public bool EnterNavigationTest()
        {
            return _enterNavigationTest != null && _enterNavigationTest();
        }
    }
}
