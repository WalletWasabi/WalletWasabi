using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using System;
using System.Reactive;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class PinLockScreenViewModel : ViewModelBase, ILockScreenViewModel
	{
		private LockScreenViewModel ParentVM { get; }

		private CompositeDisposable Disposables { get; }

		public ReactiveCommand<string, Unit> KeyPadCommand { get; }

		private ObservableAsPropertyHelper<bool> _isLocked;
		public bool IsLocked => _isLocked?.Value ?? false;

		private string _pinInput;

		public string PinInput
		{
			get => _pinInput;
			set => this.RaiseAndSetIfChanged(ref _pinInput, value);
		}

		private bool _warningMessageVisible;

		public bool WarningMessageVisible
		{
			get => _warningMessageVisible;
			set => this.RaiseAndSetIfChanged(ref _warningMessageVisible, value);
		}

		public PinLockScreenViewModel(LockScreenViewModel lockScreenViewModel)
		{
			ParentVM = Guard.NotNull(nameof(lockScreenViewModel), lockScreenViewModel);

			Disposables = new CompositeDisposable();

			KeyPadCommand = ReactiveCommand.Create<string>((arg) =>
			{
				if (arg == "BACK")
				{
					if (PinInput.Length > 0)
					{
						PinInput = PinInput.Substring(0, PinInput.Length - 1);
						WarningMessageVisible = false;
					}
				}
				else if (arg == "CLEAR")
				{
					PinInput = string.Empty;
					WarningMessageVisible = false;
				}
				else
				{
					PinInput += arg;
				}
			});

			this.WhenAnyValue(x => x.PinInput)
				.Throttle(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (string.IsNullOrWhiteSpace(x))
					{
						WarningMessageVisible = false;
					}
					else if (ParentVM.PinHash == HashHelpers.GenerateSha256Hash(x))
					{
						WarningMessageVisible = false;
					}
					else
					{
						WarningMessageVisible = true;
					}
				});

			this.WhenAnyValue(x => x.PinInput)
				.Select(Guard.Correct)
				.Where(x => x != string.Empty)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (ParentVM.PinHash == HashHelpers.GenerateSha256Hash(x))
					{
						ParentVM.IsLocked = false;
						PinInput = string.Empty;
					}
				});

			_isLocked = ParentVM
				.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.ToProperty(this, x => x.IsLocked)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.IsLocked)
				.Where(x => !x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => PinInput = string.Empty);
		}

		public void Dispose()
		{
			Disposables?.Dispose();
		}
	}
}
