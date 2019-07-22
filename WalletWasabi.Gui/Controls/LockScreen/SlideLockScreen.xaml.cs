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

		public static readonly DirectProperty<SlideLockScreen, double> OffsetProperty =
					AvaloniaProperty.RegisterDirect<SlideLockScreen, double>(nameof(Offset),
															  o => o.Offset,
															  (o, v) => o.Offset = v);

		private double _offset;

		public double Offset
		{
			get => _offset;
			set => SetAndRaise(OffsetProperty, ref _offset, value);
		}

		public static readonly DirectProperty<SlideLockScreen, bool> DoneAnimatingProperty =
			AvaloniaProperty.RegisterDirect<SlideLockScreen, bool>(nameof(DoneAnimating),
													  o => o.DoneAnimating,
													  (o, v) => o.DoneAnimating = v);

		private bool _doneAnimating;

		public bool DoneAnimating
		{
			get => _doneAnimating;
			set => SetAndRaise(DoneAnimatingProperty, ref _doneAnimating, value);
		}

		private TranslateTransform TargetTransform { get; } = new TranslateTransform();
		private Thumb DragThumb { get; }

		public void OnDataContextChanged()
		{
			var vm = this.DataContext as SlideLockScreenViewModel;

			this.WhenAnyValue(x => x.Offset)
			  .Subscribe(x => TargetTransform.Y = x)
			  .DisposeWith(vm.Disposables);

			this.WhenAnyValue(x => x.Bounds)
				.Select(x => x.Height)
				.Subscribe(x => vm.BoundsHeight = x)
				.DisposeWith(vm.Disposables);

			this.WhenAnyValue(x => x.DoneAnimating)
				.Subscribe(x =>
				{
					if (x) vm.StateChanged = false;
				})
				.DisposeWith(vm.Disposables);

			Observable.FromEventPattern(DragThumb, nameof(DragThumb.DragCompleted))
					  .Subscribe(e => vm.IsUserDragging = false)
					  .DisposeWith(vm.Disposables);

			Observable.FromEventPattern(DragThumb, nameof(DragThumb.DragStarted))
					  .Subscribe(e => vm.IsUserDragging = true)
					  .DisposeWith(vm.Disposables);

			Observable.FromEventPattern<VectorEventArgs>(DragThumb, nameof(DragThumb.DragDelta))
					  .Where(e => e.EventArgs.Vector.Y < 0)
					  .Select(e => e.EventArgs.Vector.Y)
					  .Subscribe(x => vm.Offset = x)
					  .DisposeWith(vm.Disposables);

			vm.WhenAnyValue(x => x.StateChanged)
				.Subscribe(x =>
				{
					if (x)
					{
						this.Classes.Add("statechanged");
					}
					else
					{
						this.Classes.Remove("statechanged");
					}
				})
				.DisposeWith(vm.Disposables);

			Clock.Subscribe(vm.OnClockTick)
				 .DisposeWith(vm.Disposables);
		}

		public SlideLockScreen() : base()
		{
			InitializeComponent();

			DragThumb = this.FindControl<Thumb>("PART_DragThumb");
			this.FindControl<Grid>("Shade").RenderTransform = TargetTransform;

			this.DataContextChanged += delegate
			{
				if (this.DataContext is SlideLockScreenViewModel)
					OnDataContextChanged();
			};
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
