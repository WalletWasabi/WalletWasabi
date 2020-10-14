using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class SplitViewAutoBehavior : Behavior<SplitView>
	{
		private bool _sidebarWasForceClosed;

		private CompositeDisposable Disposables { get; set; }

		public static readonly StyledProperty<double> CollapseThresholdProperty =
			AvaloniaProperty.Register<SplitViewAutoBehavior, double>(nameof(CollapseThreshold));

		public static readonly StyledProperty<Action> ToggleActionProperty =
			AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(ToggleAction));

		public static readonly StyledProperty<Action> CollapseOnClickActionProperty =
			AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(CollapseOnClickAction));

		public double CollapseThreshold
		{
			get => GetValue(CollapseThresholdProperty);
			set => SetValue(CollapseThresholdProperty, value);
		}

		public Action ToggleAction
		{
			get => GetValue(ToggleActionProperty);
			set => SetValue(ToggleActionProperty, value);
		}

		public Action CollapseOnClickAction
		{
			get => GetValue(CollapseOnClickActionProperty);
			set => SetValue(CollapseOnClickActionProperty, value);
		}

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable
			{
				AssociatedObject.WhenAnyValue(x => x.Bounds)
				.DistinctUntilChanged()
				.Subscribe(SplitViewBoundsChanged)
			};

			ToggleAction = OnToggleAction;
			CollapseOnClickAction = OnCollapseOnClickAction;

			base.OnAttached();
		}

		private void OnCollapseOnClickAction()
		{
			if (AssociatedObject.Bounds.Width <= CollapseThreshold && AssociatedObject.IsPaneOpen)
			{
				AssociatedObject.IsPaneOpen = false;
			}
		}

		private void OnToggleAction()
		{
			if (AssociatedObject.Bounds.Width > CollapseThreshold)
			{
				_sidebarWasForceClosed = AssociatedObject.IsPaneOpen;
			}

			AssociatedObject.IsPaneOpen = !AssociatedObject.IsPaneOpen;
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

				if (!_sidebarWasForceClosed && AssociatedObject.IsPaneOpen)
				{
					AssociatedObject.IsPaneOpen = false;
				}
			}
			else
			{
				AssociatedObject.DisplayMode = SplitViewDisplayMode.CompactInline;

				if (!_sidebarWasForceClosed && !AssociatedObject.IsPaneOpen)
				{
					AssociatedObject.IsPaneOpen = true;
				}
			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
