using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.LockScreen;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Gui.ViewModels
{
    public class LockScreenViewModel : ViewModelBase
    {
        private CompositeDisposable Disposables { get; } = new CompositeDisposable();

        public Global Global { get; }

        private LockScreenImpl _activeLockScreen;

        public LockScreenImpl ActiveLockScreen
        {
            get => _activeLockScreen;
            set => this.RaiseAndSetIfChanged(ref _activeLockScreen, value);
        }

        public LockScreenViewModel(Global global)
        {
            Global = global;
        }

        public void Initialize()
        {
            Global.UiConfig.WhenAnyValue(x => x.LockScreenActive)
                           .Subscribe(OnLockScreenActiveChange)
                           .DisposeWith(Disposables);

            Global.UiConfig.WhenAnyValue(x => x.LockScreenType)
                           .Subscribe(OnLockScreenTypeChange)
                           .DisposeWith(Disposables);

            OnLockScreenTypeChange(Global.UiConfig.LockScreenType);

            ActiveLockScreen.WhenAnyValue(x => x.IsLocked)
                            .Subscribe(ActiveLockScreenIsLockedChanged)
                            .DisposeWith(Disposables);

        }

        private void ActiveLockScreenIsLockedChanged(bool obj)
        {
            if (Global.UiConfig.LockScreenActive != obj)
                Global.UiConfig.LockScreenActive = obj;
        }

        // Registers oncoming changes from settings
        private void OnLockScreenActiveChange(bool? obj)
        {
            if (ActiveLockScreen is null) return;

            if (ActiveLockScreen.IsLocked != obj)
                ActiveLockScreen.IsLocked = obj ?? false;
        }

        private void OnLockScreenTypeChange(LockScreenType? obj)
        {
            switch (obj)
            {
                case LockScreenType.Simple:
                    ActiveLockScreen = new SimpleLock();
                    break;
                case LockScreenType.SlideLock:
                    ActiveLockScreen = new SlideLock();
                    break;
                case LockScreenType.PINLock:

                    break;
            }

        }


        #region IDisposable Support

        private volatile bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Disposables?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion IDisposable Support
    }
}
