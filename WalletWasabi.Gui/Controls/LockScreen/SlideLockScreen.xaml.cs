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
			AvaloniaProperty.RegisterDirect<SlideLockScreen, bool>(nameof(IsLocked), o => o.IsLocked, (o, v) => o.IsLocked = v);

		private bool _isLocked;

		public bool IsLocked
		{
			get => _isLocked;
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public static readonly DirectProperty<SlideLockScreen, double> OffsetProperty =
			AvaloniaProperty.RegisterDirect<SlideLockScreen, double>(nameof(Offset), o => o.Offset, (o, v) => o.Offset = v);

		private double _offset;

		public double Offset
		{
			get => _offset;
			set => SetAndRaise(OffsetProperty, ref _offset, value);
		}

		public static readonly DirectProperty<SlideLockScreen, bool> DoneAnimatingProperty =
			AvaloniaProperty.RegisterDirect<SlideLockScreen, bool>(nameof(DoneAnimating), o => o.DoneAnimating, (o, v) => o.DoneAnimating = v);

		private bool _doneAnimating;

		public bool DoneAnimating
		{
			get => _doneAnimating;
			set => SetAndRaise(DoneAnimatingProperty, ref _doneAnimating, value);
		}

		private TranslateTransform TargetTransform { get; } = new TranslateTransform();
		private Thumb DragThumb { get; }

		public SlideLockScreen() : base()
		{
			InitializeComponent();

			DragThumb = this.FindControl<Thumb>("PART_DragThumb");
			this.FindControl<Grid>("Shade").RenderTransform = TargetTransform;

			DataContextChanged += delegate
			{
				if (DataContext is SlideLockScreenViewModel)
				{
					OnDataContextChanged();
				}
			};
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public void OnDataContextChanged()
		{
			var vm = DataContext as SlideLockScreenViewModel;

			this.WhenAnyValue(x => x.Offset)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => TargetTransform.Y = x)
				.DisposeWith(vm.Disposables);

			this.WhenAnyValue(x => x.Bounds)
				.Select(x => x.Height)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => vm.BoundsHeight = x)
				.DisposeWith(vm.Disposables);

			this.WhenAnyValue(x => x.DoneAnimating)
				.Where(x => x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					vm.StateChanged = false;
				})
				.DisposeWith(vm.Disposables);

			Observable.FromEventPattern(DragThumb, nameof(DragThumb.DragCompleted))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => vm.IsUserDragging = false)
				.DisposeWith(vm.Disposables);

			Observable.FromEventPattern(DragThumb, nameof(DragThumb.DragStarted))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => vm.IsUserDragging = true)
				.DisposeWith(vm.Disposables);

			Observable.FromEventPattern<VectorEventArgs>(DragThumb, nameof(DragThumb.DragDelta))
				.Where(e => e.EventArgs.Vector.Y < 0)
				.Select(e => e.EventArgs.Vector.Y)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => vm.Offset = x)
				.DisposeWith(vm.Disposables);

			vm.WhenAnyValue(x => x.StateChanged)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x)
					{
						Classes.Add("statechanged");
					}
					else
					{
						Classes.Remove("statechanged");
					}
				})
				.DisposeWith(vm.Disposables);

			Clock.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(vm.OnClockTick)
				.DisposeWith(vm.Disposables);
		}
	}
}
