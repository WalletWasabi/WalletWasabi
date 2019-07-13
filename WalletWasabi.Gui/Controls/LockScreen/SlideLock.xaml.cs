using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using Avalonia;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	internal class SlideLock : LockScreenBase
	{
		private CompositeDisposable Disposables { get; set; }
		private Grid Shade;
		private Thumb DragThumb;
		private TranslateTransform TargetTransform;
		private bool UserDragInProgress, StopSpring;

		private const double ThresholdPercent = 1 / 6d;
		private double RealThreshold;
		private const double Stiffness = 0.12d;

		private double _offset = 0;
		private double Offset
		{
			get => _offset;
			set => OnOffsetChanged(value);
		}

		public SlideLock() : base()
		{
			InitializeComponent();

			this.Shade = this.FindControl<Grid>("Shade");
			this.DragThumb = this.FindControl<Thumb>("PART_DragThumb");

			TargetTransform = new TranslateTransform();
			Shade.RenderTransform = TargetTransform;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void OnBoundsChange(Rect obj)
		{
			var newHeight = obj.Height;
			RealThreshold = newHeight * ThresholdPercent;
		}

		private void OnOffsetChanged(double value)
		{
			_offset = value;
			TargetTransform.Y = _offset;
		}

		private void OnClockTick(TimeSpan CurrentTime)
		{
			if (!UserDragInProgress & Math.Abs(Offset) > RealThreshold)
			{
				IsLocked = false;
				StopSpring = true;
				return;
			}

			if (IsLocked & !UserDragInProgress & Offset != 0)
			{
				Offset *= 1 - Stiffness;
			}
		}

		private void OnDragStarted()
		{
			UserDragInProgress = true;
		}

		private void OnDragDelta(VectorEventArgs e)
		{
			if (e.Vector.Y < 0)
			{
				this.Offset = e.Vector.Y;
			}
		}

		private void OnDragCompleted(VectorEventArgs e)
		{
			UserDragInProgress = false;
		}

		public override void DoLock()
		{
			Shade.Classes.Add("Locked");
			Shade.Classes.Remove("Unlocked");
			Offset = 0;
			UserDragInProgress = false;

			Disposables = new CompositeDisposable();

			Observable.FromEventPattern<VectorEventArgs>(DragThumb, nameof(DragThumb.DragCompleted))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Subscribe(e => OnDragCompleted(e.EventArgs))
					  .DisposeWith(Disposables);

			Observable.FromEventPattern<VectorEventArgs>(DragThumb, nameof(DragThumb.DragDelta))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Subscribe(e => OnDragDelta(e.EventArgs))
					  .DisposeWith(Disposables);

			Observable.FromEventPattern(DragThumb, nameof(DragThumb.DragStarted))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Subscribe(e => OnDragStarted())
					  .DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Bounds)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(OnBoundsChange)
				.DisposeWith(Disposables);

			Clock.Subscribe(OnClockTick)
				 .DisposeWith(Disposables);
		}

		public override void DoUnlock()
		{
			Shade.Classes.Add("Unlocked");
			Shade.Classes.Remove("Locked");
			Disposables?.Dispose();
		}
	}
}
