using System;
using ReactiveUI;
using System.Reactive.Disposables;
using WalletWasabi.Helpers;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class LockScreenViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; }

		public Global Global { get; }

		public LockScreenViewModel(Global global)
		{
			Global = Guard.NotNull(nameof(Global), global);
			Disposables = new CompositeDisposable();
		}

		private ILockScreenViewModel _activeLockScreen;
		public ILockScreenViewModel ActiveLockScreen
		{
			get => _activeLockScreen;
			set => this.RaiseAndSetIfChanged(ref _activeLockScreen, value);
		}

		private ObservableAsPropertyHelper<string> _pinHash;
		public string PINHash => _pinHash?.Value ?? default;

		private bool _isLocked;
		public bool IsLocked
		{
			get => _isLocked;
			set => this.RaiseAndSetIfChanged(ref _isLocked, value);
		}

		public void Initialize()
		{
			Global.UiConfig.WhenAnyValue(x => x.LockScreenActive)
						   .ObserveOn(RxApp.MainThreadScheduler)
						   .BindTo(this, y => y.IsLocked)
						   .DisposeWith(Disposables);

			this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(Global.UiConfig, y => y.LockScreenActive)
				.DisposeWith(Disposables);

			_pinHash = Global.UiConfig
							 .WhenAnyValue(x => x.LockScreenPinHash)
							 .ObserveOn(RxApp.MainThreadScheduler)
							 .Do(x => CheckLockScreenType(x))
							 .ToProperty(this, x => x.PINHash);
		}

		private void CheckLockScreenType(string currentHash)
		{
			ActiveLockScreen?.Dispose();

			if (currentHash != string.Empty)
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