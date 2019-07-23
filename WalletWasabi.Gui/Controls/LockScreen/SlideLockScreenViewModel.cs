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
		private enum SlideLockState
		{
			Idle,
			UserIsDragging,
			UserDragPassedThreshold,
		}

		private SlideLockState _state = SlideLockState.Idle;
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

		private double _boundsHeight;

		public double BoundsHeight
		{
			get => _boundsHeight;
			set => this.RaiseAndSetIfChanged(ref _boundsHeight, value);
		}

		public double _targetOffset;

		public double TargetOffset
		{
			get => _targetOffset;
			set => this.RaiseAndSetIfChanged(ref _targetOffset, value);
		}

		public double Threshold => BoundsHeight * ThresholdPercent;

		private double _targetOpacity;

		public double TargetOpacity
		{
			get => _targetOpacity;
			set => this.RaiseAndSetIfChanged(ref _targetOpacity, value);
		}

		private double _offset;

		public double Offset
		{
			get => _offset;
			set => this.RaiseAndSetIfChanged(ref _offset, value);
		}

		private bool _stateChanged;

		public bool StateChanged
		{
			get => _stateChanged;
			set => this.RaiseAndSetIfChanged(ref _stateChanged, value);
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

			this.WhenAnyValue(x => x.TargetOffset)
				.DistinctUntilChanged()
				.Subscribe(x =>
				{
					StateChanged = false;
					StateChanged = true;
				})
				.DisposeWith(Disposables);
		}

		public void Dispose()
		{
			Disposables?.Dispose();
		}

		internal void OnClockTick(TimeSpan _)
		{
			switch (_state)
			{
				case SlideLockState.Idle:

					if (IsUserDragging)
					{
						_state = SlideLockState.UserIsDragging;
						return;
					}

					if (IsLocked)
					{
						TargetOffset = 0;
						TargetOpacity = 1;
						Offset *= 1 - Stiffness;
					}
					else
					{
						TargetOpacity = 0;
						TargetOffset = -BoundsHeight;
					}

					break;

				case SlideLockState.UserIsDragging:
					if (!IsUserDragging)
					{
						if (Math.Abs(Offset) > Threshold)
						{
							_state = SlideLockState.UserDragPassedThreshold;
						}
						else
						{
							_state = SlideLockState.Idle;
						}
					}
					break;

				case SlideLockState.UserDragPassedThreshold:
					_parentVM.IsLocked = false;
					_state = SlideLockState.Idle;
					break;
			}
		}
	}
}
