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
	public class SlideLockScreen : UserControl
	{
		public static readonly DirectProperty<SlideLockScreen, bool> IsLockedProperty =
			AvaloniaProperty.RegisterDirect<SlideLockScreen, bool>(nameof(IsLocked),
													  o => o.IsLocked,
													  (o, v) => o.IsLocked = v);

		private bool _isLocked;

		public bool IsLocked
		{
			get => _isLocked;
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public static readonly DirectProperty<SlideLockScreen, string> TokenProperty =
			AvaloniaProperty.RegisterDirect<SlideLockScreen, string>(nameof(Token),
													  o => o.Token,
													  (o, v) => o.Token = v);

		private string _token;

		public string Token
		{
			get => _token;
			set => SetAndRaise(TokenProperty, ref _token, value);
		}

		public CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private TranslateTransform TargetTransform { get; } = new TranslateTransform();
		private Thumb DragThumb { get; }

		private bool _userDragInProgress;
		private double _realThreshold;
		private const double ThresholdPercent = 1 / 6d;
		private const double Stiffness = 0.12d;

		private double _offset = 0;

		private double Offset
		{
			get => _offset;
			set => OnOffsetChanged(value);
		}

		public SlideLockScreen() : base()
		{
			InitializeComponent();

			DragThumb = this.FindControl<Thumb>("PART_DragThumb");
			this.FindControl<Grid>("Shade").RenderTransform = TargetTransform;

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

			this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Where(x => x)
				.Subscribe(x => Offset = 0)
				.DisposeWith(Disposables);

			DetachedFromLogicalTree += delegate
			{
				Disposables?.Dispose();
			};
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void OnBoundsChange(Rect obj)
		{
			var newHeight = obj.Height;
			_realThreshold = newHeight * ThresholdPercent;
		}

		private void OnOffsetChanged(double value)
		{
			_offset = value;
			TargetTransform.Y = _offset;
		}

		private void OnClockTick(TimeSpan CurrentTime)
		{
			if (IsLocked & !_userDragInProgress & Math.Abs(Offset) > _realThreshold)
			{
				Token = "Unlock";
				return;
			}
			else if (IsLocked & !_userDragInProgress & Offset != 0)
			{
				Offset *= 1 - Stiffness;
			}
		}

		private void OnDragStarted()
		{
			_userDragInProgress = true;
		}

		private void OnDragDelta(VectorEventArgs e)
		{
			if (e.Vector.Y < 0)
			{
				Offset = e.Vector.Y;
			}
		}

		private void OnDragCompleted(VectorEventArgs e)
		{
			_userDragInProgress = false;
		}
	}
}
