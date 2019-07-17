using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLockScreenViewModel : ViewModelBase, ILockScreenViewModel
	{
		public CompositeDisposable Disposables { get; }
		private LockScreenViewModel _parentVM;
		private ObservableAsPropertyHelper<bool> _isLocked;
		public bool IsLocked => _isLocked?.Value ?? false;

		private bool _isUserDragging;
		public bool IsUserDragging
		{
			get => _isUserDragging;
			set => this.RaiseAndSetIfChanged(ref _isUserDragging, value);
		}

		private double _threshold;
		public double Threshold
		{
			get => _threshold;
			set => this.RaiseAndSetIfChanged(ref _threshold, value);
		}
		
		private double _offset;
		public double Offset
		{
			get => _offset;
			set => this.RaiseAndSetIfChanged(ref _offset, value);
		}

		public readonly double ThresholdPercent = 1 / 6d;
		public readonly double Stiffness = 0.12d;

		public SlideLockScreenViewModel(LockScreenViewModel lockScreenViewModel)
		{
			_parentVM = Guard.NotNull(nameof(lockScreenViewModel), lockScreenViewModel);

			Disposables = new CompositeDisposable();

			_isLocked = _parentVM.WhenAnyValue(x => x.IsLocked)
								 .ObserveOn(RxApp.MainThreadScheduler)
								 .ToProperty(this, x => x.IsLocked)
								 .DisposeWith(Disposables);
		}

		public void Dispose()
		{
			Disposables?.Dispose();
		}

		internal void OnClockTick(TimeSpan obj)
		{
			if (IsLocked & !IsUserDragging & Math.Abs(Offset) > Threshold)
			{
				_parentVM.IsLocked = false;
				return;
			}
			else if (IsLocked & !IsUserDragging & Offset != 0)
			{
				Offset *= 1 - Stiffness;
			}
		}
	}
}
