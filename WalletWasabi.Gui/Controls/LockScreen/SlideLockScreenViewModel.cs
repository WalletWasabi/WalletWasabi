using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLockScreenViewModel : ViewModelBase, ILockScreenViewModel
	{
		private LockScreenViewModel _parentVM;

		private CompositeDisposable Disposables { get; }

		private ObservableAsPropertyHelper<bool> _isLocked;
		public bool IsLocked => _isLocked?.Value ?? false;

		private string _token;

		public string Token
		{
			get => _token;
			set => this.RaiseAndSetIfChanged(ref _token, value);
		}

		public SlideLockScreenViewModel(LockScreenViewModel lockScreenViewModel)
		{
			_parentVM = Guard.NotNull(nameof(lockScreenViewModel), lockScreenViewModel);

			Disposables = new CompositeDisposable();

			this.WhenAnyValue(x => x.Token)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(CheckToken)
				.DisposeWith(Disposables);

			_isLocked = _parentVM.WhenAnyValue(x => x.IsLocked)
								 .ObserveOn(RxApp.MainThreadScheduler)
								 .ToProperty(this, x => x.IsLocked)
								 .DisposeWith(Disposables);
		}

		private void CheckToken(string input)
		{
			if (input == "Unlock")
			{
				_parentVM.IsLocked = false;
				Token = string.Empty;
			}
		}

		public void Dispose()
		{
			Disposables?.Dispose();
		}
	}
}
