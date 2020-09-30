using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class SplitViewAutoBehavior : Behavior<SplitView>
	{
		private CompositeDisposable Disposables { get; set; }

		public static readonly StyledProperty<double> CollapseThresholdProperty =
			AvaloniaProperty.Register<SplitViewAutoBehavior, double>(nameof(CollapseThreshold));

		public double CollapseThreshold
		{
			get => GetValue(CollapseThresholdProperty);
			set => SetValue(CollapseThresholdProperty, value);
		}

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			Disposables.Add(AssociatedObject.WhenAnyValue(x => x.Bounds)
				.DistinctUntilChanged()
				.Subscribe(SplitViewBoundsChanged));

			base.OnAttached();
		}

		private void SplitViewBoundsChanged(Rect x)
		{
			if (AssociatedObject is null)
			{
				return;
			}
			
			if (x.Width <= CollapseThreshold)
			{
				AssociatedObject.DisplayMode = SplitViewDisplayMode.CompactOverlay;

				if (AssociatedObject.IsPaneOpen)
				{
					AssociatedObject.IsPaneOpen = false;
				}
			}
			else
			{
				AssociatedObject.DisplayMode = SplitViewDisplayMode.CompactInline;
			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
