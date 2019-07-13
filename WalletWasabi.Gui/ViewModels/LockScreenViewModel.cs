using System;
using ReactiveUI;
using System.Reactive.Disposables;
using WalletWasabi.Gui.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.ViewModels
{
    public class LockScreenViewModel : ViewModelBase
    {
        private CompositeDisposable Disposables { get; } = new CompositeDisposable();

        public Global Global { get; }

        public LockScreenViewModel(Global global)
        {
            Global = Guard.NotNull(nameof(Global), global);
        }

        private LockScreenType _lockScreenType;
        public LockScreenType ActiveLockScreen
        {
            get => _lockScreenType;
            set => this.RaiseAndSetIfChanged(ref _lockScreenType, value);
        }

        private bool _isLocked;
        public bool IsLocked
        {
            get => _isLocked;
            set => this.RaiseAndSetIfChanged(ref _isLocked, value);
        }

        private string _pinHash;
        public string PINHash
        {
            get => _pinHash;
            set => this.RaiseAndSetIfChanged(ref _pinHash, value);
        }

        public void Initialize()
        {
            Global.UiConfig.WhenAnyValue(x => x.LockScreenActive)
                           .BindTo(this, y => y.IsLocked)
                           .DisposeWith(Disposables);

            this.WhenAnyValue(x => x.IsLocked)
                           .BindTo(Global.UiConfig, y => y.LockScreenActive)
                           .DisposeWith(Disposables);

            Global.UiConfig.WhenAnyValue(x => x.LockScreenPinHash)
                           .Subscribe(CheckLockScreenType)
                           .DisposeWith(Disposables);
        }

        private void CheckLockScreenType(string currentHash)
        {
            if (currentHash != string.Empty)
            {
                ActiveLockScreen = LockScreenType.PINLock;
            }
            else
            {
                ActiveLockScreen = default(LockScreenType);
            }

			PINHash = currentHash;
        }
    }
}