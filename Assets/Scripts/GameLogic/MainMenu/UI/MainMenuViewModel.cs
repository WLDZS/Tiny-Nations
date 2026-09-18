using System;
using BorFramework;

namespace GameLogic.MainMenu.UI
{
    public sealed class MainMenuViewModel : UIViewModelBase
    {
        private readonly Func<bool> _enterDemo;

        public MainMenuViewModel(Func<bool> enterDemo)
        {
            _enterDemo = enterDemo;
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
    }
}
