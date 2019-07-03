using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    internal class LockScreen : UserControl
    {
        private bool _isLocked;
        private LockScreenType _activeLockScreenType;
        private ContentControl LockScreenHost;
        private LockScreenImpl CurrentLockScreen;


        public static readonly DirectProperty<LockScreen, LockScreenType> ActiveLockScreenTypeProperty =
            AvaloniaProperty.RegisterDirect<LockScreen, LockScreenType>(nameof(ActiveLockScreenType),
                                                                        o => o.ActiveLockScreenType,
                                                                        (o, v) => o.ActiveLockScreenType = v);
        public LockScreenType ActiveLockScreenType
        {
            get => _activeLockScreenType;
            set => this.SetAndRaise(ActiveLockScreenTypeProperty, ref _activeLockScreenType, value);
        }

        public static readonly DirectProperty<LockScreen, bool> IsLockedProperty =
            AvaloniaProperty.RegisterDirect<LockScreen, bool>(nameof(IsLocked),
                                                              o => o.IsLocked,
                                                              (o, v) => o.IsLocked = v);
        public bool IsLocked
        {
            get => _isLocked;
            set => this.SetAndRaise(IsLockedProperty, ref _isLocked, value);
        }

        public LockScreen()
        {
            InitializeComponent();

            this.LockScreenHost = this.FindControl<ContentControl>("LockScreenHost");

            this.WhenAnyValue(x => x.ActiveLockScreenType)
                .Subscribe(OnActiveLockScreenTypeChanged);

            this.WhenAnyValue(x => x.IsLocked)
                .Subscribe(y =>
                {
                    if (CurrentLockScreen is null) return;
                    CurrentLockScreen.IsLocked = y;
                });
        }

        private void OnActiveLockScreenTypeChanged(LockScreenType obj)
        {
            switch (obj)
            {
                case LockScreenType.Simple:

                    CurrentLockScreen = new SimpleLock();
                    LockScreenHost.Content = CurrentLockScreen;
                    CurrentLockScreen.IsLocked = true;
                    break;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
