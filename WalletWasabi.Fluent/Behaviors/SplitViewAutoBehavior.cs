using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class SplitViewAutoBehavior : DisposingBehavior<SplitView>
{
	private bool _sidebarWasForceClosed;

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

	protected override void OnAttached(CompositeDisposable disposables)
	{
		AssociatedObject!.WhenAnyValue(x => x.Bounds)
			.DistinctUntilChanged()
			.Subscribe(SplitViewBoundsChanged)
			.DisposeWith(disposables);

		ToggleAction = OnToggleAction;
		CollapseOnClickAction = OnCollapseOnClickAction;
	}

	private void OnCollapseOnClickAction()
	{
		if (AssociatedObject!.Bounds.Width <= CollapseThreshold && AssociatedObject!.IsPaneOpen)
		{
			AssociatedObject!.IsPaneOpen = false;
		}
	}

	private void OnToggleAction()
	{
		if (AssociatedObject!.Bounds.Width > CollapseThreshold)
		{
			_sidebarWasForceClosed = AssociatedObject!.IsPaneOpen;
		}

		AssociatedObject!.IsPaneOpen = !AssociatedObject!.IsPaneOpen;
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
}
