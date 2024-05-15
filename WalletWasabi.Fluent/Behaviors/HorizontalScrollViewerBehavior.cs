using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

internal class HorizontalScrollViewerBehavior : Behavior<ScrollViewer>
{
	public enum ChangeSize
	{
		Line,
		Page
	}

	public static readonly StyledProperty<bool> IsEnabledProperty =
		AvaloniaProperty.Register<HorizontalScrollViewerBehavior, bool>(nameof(IsEnabled), true);

	public static readonly StyledProperty<bool> RequireShiftKeyProperty =
		AvaloniaProperty.Register<HorizontalScrollViewerBehavior, bool>(nameof(RequireShiftKey));

	public static readonly StyledProperty<ChangeSize> ScrollChangeSizeProperty =
		AvaloniaProperty.Register<HorizontalScrollViewerBehavior, ChangeSize>(nameof(ScrollChangeSize));

	public bool IsEnabled
	{
		get => GetValue(IsEnabledProperty);
		set => SetValue(IsEnabledProperty, value);
	}

	public bool RequireShiftKey
	{
		get => GetValue(RequireShiftKeyProperty);
		set => SetValue(RequireShiftKeyProperty, value);
	}

	public ChangeSize ScrollChangeSize
	{
		get => GetValue(ScrollChangeSizeProperty);
		set => SetValue(ScrollChangeSizeProperty, value);
	}

	protected override void OnAttached()
	{
		base.OnAttached();

		AssociatedObject!.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		AssociatedObject!.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
	}

	private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		if (!IsEnabled)
		{
			e.Handled = true;
			return;
		}

		if ((RequireShiftKey && e.KeyModifiers == KeyModifiers.Shift) || !RequireShiftKey)
		{
			if (e.Delta.Y < 0)
			{
				if (ScrollChangeSize == ChangeSize.Line)
				{
					AssociatedObject!.LineRight();
				}
				else
				{
					AssociatedObject!.PageRight();
				}
			}
			else
			{
				if (ScrollChangeSize == ChangeSize.Line)
				{
					AssociatedObject!.LineLeft();
				}
				else
				{
					AssociatedObject!.PageLeft();
				}
			}
		}
	}
}
