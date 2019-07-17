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
		private LockScreenViewModel _parentVM;

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
			_parentVM = Guard.NotNull(nameof(lockScreenViewModel), lockScreenViewModel);

			Disposables = new CompositeDisposable();

			KeyPadCommand = ReactiveCommand.Create<string>((arg) =>
			{
				PinInput += arg;
			});

			this.WhenAnyValue(x => x.PinInput)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Throttle(TimeSpan.FromSeconds(0.5))
				.Select(Guard.Correct)
				.Where(x => x != string.Empty)
				.Do(x => WarningMessageVisible = false)
				.DistinctUntilChanged()
				.Subscribe(CheckPIN)
				.DisposeWith(Disposables);

			_isLocked = _parentVM.WhenAnyValue(x => x.IsLocked)
								 .ObserveOn(RxApp.MainThreadScheduler)
								 .ToProperty(this, x => x.IsLocked)
								 .DisposeWith(Disposables);
		}

		private void CheckPIN(string input)
		{
			if (_parentVM.PINHash == HashHelpers.GenerateSha256Hash(input))
			{
				_parentVM.IsLocked = false;
				PinInput = string.Empty;
			}
			else
			{
				WarningMessageVisible = true;
			}
		}

		public void Dispose()
		{
			Disposables?.Dispose();
		}
	}
}
