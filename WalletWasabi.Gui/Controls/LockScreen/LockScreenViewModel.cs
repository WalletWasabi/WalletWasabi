using System;
using ReactiveUI;
using System.Reactive.Disposables;
using WalletWasabi.Helpers;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using Splat;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class LockScreenViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; }

		public LockScreenViewModel()
		{
			Disposables = new CompositeDisposable();
		}

		private ILockScreenViewModel _activeLockScreen;

		public ILockScreenViewModel ActiveLockScreen
		{
			get => _activeLockScreen;
			set => this.RaiseAndSetIfChanged(ref _activeLockScreen, value);
		}

		private ObservableAsPropertyHelper<string> _pinHash;
		public string PinHash => _pinHash?.Value ?? default;

		private bool _isLocked;

		public bool IsLocked
		{
			get => _isLocked;
			set => this.RaiseAndSetIfChanged(ref _isLocked, value);
		}

		public void Initialize()
		{
			var global = Locator.Current.GetService<Global>();

			global.UiConfig
				.WhenAnyValue(x => x.LockScreenActive)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(this, y => y.IsLocked)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(global.UiConfig, y => y.LockScreenActive)
				.DisposeWith(Disposables);

			_pinHash = global.UiConfig
				.WhenAnyValue(x => x.LockScreenPinHash)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Do(x => CheckLockScreenType(x))
				.ToProperty(this, x => x.PinHash);
		}

		private void CheckLockScreenType(string currentHash)
		{
			ActiveLockScreen?.Dispose();

			if (currentHash.Length != 0)
			{
				ActiveLockScreen = new PinLockScreenViewModel(this);
			}
			else
			{
				ActiveLockScreen = new SlideLockScreenViewModel(this);
			}
		}
	}
}
