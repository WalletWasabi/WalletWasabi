using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLockScreenViewModel : LockScreenViewModelBase
	{
		private enum SlideLockState
		{
			Idle,
			UserIsDragging,
			UserDragPassedThreshold
		}

		private SlideLockState _state = SlideLockState.Idle;
		

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

		private double _targetOffset;

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

		protected override void OnInitialise(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.TargetOffset)
				.DistinctUntilChanged()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					StateChanged = false;
					StateChanged = true;
				})
				.DisposeWith(disposables);
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
						_state = Math.Abs(Offset) > Threshold ? SlideLockState.UserDragPassedThreshold : SlideLockState.Idle;
					}
					break;

				case SlideLockState.UserDragPassedThreshold:
					IsLocked = false;
					_state = SlideLockState.Idle;
					break;
			}
		}
	}
}
