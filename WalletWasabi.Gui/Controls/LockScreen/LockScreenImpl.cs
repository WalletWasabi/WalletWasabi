using System;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public abstract class LockScreenImpl : UserControl
    {
        private bool _isLocked;

        public static readonly DirectProperty<LockScreenImpl, bool> IsLockedProperty =
            AvaloniaProperty.RegisterDirect<LockScreenImpl, bool>(nameof(IsLocked), 
																  o => o.IsLocked,
																  (o,v) => o.IsLocked = v);
        public bool IsLocked
        {
            get => _isLocked;
            set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
        }
    }
}
