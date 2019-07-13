using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public class LockScreen : UserControl
    {
        private bool _isLocked;
        private string _pinHash;

        private LockScreenType _activeLockScreenType;
        private ContentControl LockScreenHost;
        private LockScreenBase CurrentLockScreen;
        private CompositeDisposable ScreenImplDisposables;

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

        public static readonly DirectProperty<LockScreen, string> PINHashProperty =
            AvaloniaProperty.RegisterDirect<LockScreen, string>(nameof(PINHash),
                                                              o => o.PINHash,
                                                              (o, v) => o.PINHash = v);
        public string PINHash
        {
            get => _pinHash;
            set => this.SetAndRaise(PINHashProperty, ref _pinHash, value);
        }

        public LockScreen()
        {
            InitializeComponent();

            this.LockScreenHost = this.FindControl<ContentControl>("LockScreenHost");

            this.WhenAnyValue(x => x.ActiveLockScreenType)
				.ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(OnActiveLockScreenTypeChanged);
        }

        private void OnActiveLockScreenTypeChanged(LockScreenType obj)
        {
            ScreenImplDisposables?.Dispose();
            ScreenImplDisposables = new CompositeDisposable();

            switch (obj)
            {
                case LockScreenType.PINLock:
                    var pinLock = new PINLock();
                    CurrentLockScreen = pinLock;

                    this.WhenAnyValue(x => x.PINHash)
						.ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(y => pinLock.PINHash = y)
                        .DisposeWith(ScreenImplDisposables);

                    break;
                default:
                    CurrentLockScreen = new SlideLock();
                    break;
            }

            LockScreenHost.Content = CurrentLockScreen;

            this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(CurrentLockScreen, y => y.IsLocked)
                .DisposeWith(ScreenImplDisposables);

            CurrentLockScreen.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(this, y => y.IsLocked)
                .DisposeWith(ScreenImplDisposables);
        } 

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
