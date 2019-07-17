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

		private TranslateTransform TargetTransform { get; } = new TranslateTransform();
		private Thumb DragThumb { get; }

		public void OnDataContextChanged()
		{
			var vm = this.DataContext as SlideLockScreenViewModel;

			vm.WhenAnyValue(x => x.Offset)
			  .Subscribe(x => TargetTransform.Y = x)
			  .DisposeWith(vm.Disposables);

			vm.WhenAnyValue(x => x.IsLocked)
			  .Where(x => x)
			  .Subscribe(x => vm.Offset = 0)
			  .DisposeWith(vm.Disposables);

			this.WhenAnyValue(x => x.Bounds)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => x.Height)
				.Subscribe(x => vm.Threshold = x * vm.ThresholdPercent)
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

			Clock.Where(x => vm.IsLocked)
				 .Subscribe(vm.OnClockTick)
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
