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
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(Guard.Correct)
				.Where(x => x != string.Empty)
				.Do(x => WarningMessageVisible = false)
				.DistinctUntilChanged()
				.Subscribe(CheckPin)
				.DisposeWith(Disposables);

			_isLocked = _parentVM.WhenAnyValue(x => x.IsLocked)
								 .ObserveOn(RxApp.MainThreadScheduler)
								 .ToProperty(this, x => x.IsLocked)
								 .DisposeWith(Disposables);
		}

		private void CheckPin(string input)
		{
			if (_parentVM.PinHash == HashHelpers.GenerateSha256Hash(input))
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
