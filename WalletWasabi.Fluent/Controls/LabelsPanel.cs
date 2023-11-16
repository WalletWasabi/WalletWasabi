using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace WalletWasabi.Fluent.Controls;

public class LabelsPanel : Panel
{
	public static readonly StyledProperty<bool> InfiniteWidthMeasureProperty =
		AvaloniaProperty.Register<LabelsPanel, bool>(nameof(InfiniteWidthMeasure));

	public static readonly StyledProperty<Control?> EllipsisControlProperty =
		AvaloniaProperty.Register<LabelsPanel, Control?>(nameof(EllipsisControl));

	public static readonly DirectProperty<LabelsPanel, List<string>?> FilteredItemsProperty =
		AvaloniaProperty.RegisterDirect<LabelsPanel, List<string>?>(
			nameof(FilteredItems),
			o => o.FilteredItems,
			(o, v) => o.FilteredItems = v);

	private List<string>? _filteredItems;
	private IDisposable? _disposable;
	private double _spacing = 2.0;
	private bool _needToTrim;

	public bool InfiniteWidthMeasure
	{
		get => GetValue(InfiniteWidthMeasureProperty);
		set => SetValue(InfiniteWidthMeasureProperty, value);
	}

	public Control? EllipsisControl
	{
		get => GetValue(EllipsisControlProperty);
		set => SetValue(EllipsisControlProperty, value);
	}

	public List<string>? FilteredItems
	{
		get => _filteredItems;
		set => SetAndRaise(FilteredItemsProperty, ref _filteredItems, value);
	}

	internal LabelsItemsPresenter? Presenter { get; set; }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == DataContextProperty)
		{
			InvalidateMeasure();
			InvalidateArrange();
		}
	}

	public override void ApplyTemplate()
	{
		base.ApplyTemplate();

		Presenter = this
			.GetLogicalAncestors()
			.FirstOrDefault(x => x is LabelsItemsPresenter) as LabelsItemsPresenter;
	}

	private void UpdateFilteredItems(int count)
	{
		if (Presenter?.ItemsSource is IEnumerable<string> items)
		{
			FilteredItems = items.Skip(count).ToList();
		}
		else
		{
			FilteredItems = new List<string>();
		}
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (EllipsisControl is { } ellipsisControl)
		{
			((ISetLogicalParent)ellipsisControl).SetParent(this);
			VisualChildren.Add(ellipsisControl);
			LogicalChildren.Add(ellipsisControl);
			_disposable = ellipsisControl.Bind(DataContextProperty, this.GetObservable(FilteredItemsProperty));
		}

		base.OnAttachedToVisualTree(e);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (EllipsisControl is { } ellipsisControl)
		{
			((ISetLogicalParent)ellipsisControl).SetParent(null);
			LogicalChildren.Remove(ellipsisControl);
			VisualChildren.Remove(ellipsisControl);
			_disposable?.Dispose();
		}

		base.OnDetachedFromVisualTree(e);
	}

	private Size MeasureOverridePanel(Size availableSize)
	{
		var num1 = 0.0;
		var num2 = 0.0;
		var visualChildren = VisualChildren;
		var count = visualChildren.Count;
		for (var index = 0; index < count; ++index)
		{
			if (visualChildren[index] is Layoutable layoutable)
			{
				layoutable.Measure(availableSize);
				num1 += layoutable.DesiredSize.Width;
				num2 = Math.Max(num2, layoutable.DesiredSize.Height);
			}
		}
		return new Size(num1, num2);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var ellipsisDesiredWidth = 0.0;
		if (EllipsisControl is { })
		{
			EllipsisControl.Measure(availableSize);
			ellipsisDesiredWidth = EllipsisControl.DesiredSize.Width;
		}

		var size = MeasureOverridePanel(availableSize.WithWidth(availableSize.Width));

		_needToTrim = !(size.Width < availableSize.Width);

		var result = CalculateWidth(
			Children,
			EllipsisControl,
			availableSize.Width,
			ellipsisDesiredWidth,
			_spacing,
			_needToTrim);

		size = size.WithWidth(result.Width);

		// TODO:
		// Currently we use quickfix to make work TreeDataGrid with ShowColumnHeaders=false
		// The width=double.MaxValue was causing measure exception (measure with infinity)
		// Quick fix for now is to limit InfiniteWidthMeasure to 10000 instead of double.MaxValue
		return InfiniteWidthMeasure ? new Size(10000, size.Height) : size;
	}

	private CalculateResult CalculateWidth(
		Avalonia.Controls.Controls children,
		Control? trimControl,
		double finalWidth,
		double trimWidth,
		double spacing,
		bool needToTrim)
	{
		var totalChildren = children.Count;
		var width = 0.0;
		var height = 0.0;
		var count = 0;
		var trim = false;

		for (var i = 0; i < totalChildren; i++)
		{
			var child = children[i];
			if (child == trimControl)
			{
				continue;
			}

			var childWidth = child.DesiredSize.Width;

			height = Math.Max(height, child.DesiredSize.Height);

			if (width + childWidth > finalWidth && needToTrim)
			{
				while (true)
				{
					if (width + trimWidth > finalWidth)
					{
						var previous = i - 1;
						if (previous >= 0)
						{
							var previousChild = children[previous];
							count--;
							width -= previousChild.DesiredSize.Width + spacing;
						}
						else
						{
							break;
						}
					}
					else
					{
						break;
					}
				}

				trim = true;

				break;
			}

			width += child.DesiredSize.Width + spacing;
			count++;
		}

		if (trim)
		{
			width += trimWidth;
		}

		return new CalculateResult(count, width, height, trim);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var spacing = _spacing;
		var trimWidth = 0.0;
		var finalWidth = finalSize.Width;
		var ellipsisControl = EllipsisControl;
		var children = Children;
		var totalChildren = children.Count;

		if (ellipsisControl is { })
		{
			trimWidth = ellipsisControl.DesiredSize.Width;
		}

		var result = CalculateWidth(
			children,
			ellipsisControl,
			finalWidth,
			trimWidth,
			spacing,
			_needToTrim);

		var offset = 0.0;

		for (var i = 0; i < totalChildren; i++)
		{
			var child = children[i];
			if (child == ellipsisControl)
			{
				continue;
			}

			if (i < result.Count)
			{
				var rect = new Rect(offset, 0.0, child.DesiredSize.Width, result.Height);
				child.Arrange(rect);
				offset += child.DesiredSize.Width + spacing;
			}
			else
			{
				child.Arrange(new Rect(-10000, -10000, 0, 0));
			}
		}

		if (ellipsisControl is { })
		{
			if (result.Trim)
			{
				var rect = new Rect(offset, 0.0, trimWidth, result.Height);
				ellipsisControl.Arrange(rect);
			}
			else
			{
				ellipsisControl.Arrange(new Rect(-10000, -10000, 0, 0));
			}
		}

		UpdateFilteredItems(result.Count);

		return new Size(result.Width, result.Height);
	}

	private record CalculateResult(int Count, double Width, double Height, bool Trim);
}
