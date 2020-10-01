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
		private bool _userSidebarIsExpanded = true;

		private CompositeDisposable Disposables { get; set; }

		public static readonly StyledProperty<double> CollapseThresholdProperty =
			AvaloniaProperty.Register<SplitViewAutoBehavior, double>(nameof(CollapseThreshold));

		public static readonly StyledProperty<Action> ToggleActionProperty =
			AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(ToggleAction));

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

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			Disposables.Add(AssociatedObject.WhenAnyValue(x => x.Bounds)
				.DistinctUntilChanged()
				.Subscribe(SplitViewBoundsChanged));

			ToggleAction = OnToggleAction;

			base.OnAttached();
		}

		private void OnToggleAction()
		{
			_userSidebarIsExpanded = !_userSidebarIsExpanded;

			AssociatedObject.IsPaneOpen = _userSidebarIsExpanded;
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
				AssociatedObject.IsPaneOpen = _userSidebarIsExpanded;
			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
