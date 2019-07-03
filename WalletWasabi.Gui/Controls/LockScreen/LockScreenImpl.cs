using System;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public abstract class LockScreenImpl : UserControl
    {
        private bool _isLocked, _currentState;

        public static readonly DirectProperty<LockScreenImpl, bool> IsLockedProperty =
            AvaloniaProperty.RegisterDirect<LockScreenImpl, bool>(nameof(IsLocked),
                                                                  o => o.IsLocked,
                                                                  (o, v) => o.IsLocked = v);

        public LockScreenImpl()
        {
            this.WhenAnyValue(x => x.IsLocked)
                .Subscribe(IsLockedChanged);
        }

        private void IsLockedChanged(bool isLocked)
        {
            if (isLocked == _currentState) return;

            _currentState = isLocked;

            if (isLocked)
            {
                this.IsHitTestVisible = true;
                DoLock();
            }
            else
            {
                this.IsHitTestVisible = false;
                DoUnlock();
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
        }

        public abstract void DoLock();
        public abstract void DoUnlock();
    }
}
