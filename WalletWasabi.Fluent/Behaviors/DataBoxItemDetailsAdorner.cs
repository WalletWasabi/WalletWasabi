using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DataBox;

namespace WalletWasabi.Fluent.Behaviors;

public class DataBoxItemDetailsAdorner : TemplatedControl
{
	public static readonly StyledProperty<DataBoxRow?> RowProperty =
		AvaloniaProperty.Register<DataBoxItemDetailsAdorner, DataBoxRow?>(nameof(Row));

	public DataBoxRow? Row
	{
		get => GetValue(RowProperty);
		set => SetValue(RowProperty, value);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (Row is null)
		{
			return availableSize;
		}

		var bounds = Row.Bounds;

		availableSize.WithHeight(bounds.Height);

		var result = availableSize;

		foreach (var visualChild in VisualChildren)
		{
			if (visualChild is Control control)
			{
				control.Measure(availableSize);
				result = control.DesiredSize;
			}
		}

		return result;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		if (Row is null)
		{
			return finalSize;
		}

		var bounds = Row.Bounds;

		finalSize.WithHeight(bounds.Height);

		var rect = new Rect(0, 0, finalSize.Width, bounds.Height);

		var result = finalSize;

		foreach (var visualChild in VisualChildren)
		{
			if (visualChild is Control control)
			{
				control.Arrange(rect);
				result = control.DesiredSize;
			}
		}

		return result;
	}
}
