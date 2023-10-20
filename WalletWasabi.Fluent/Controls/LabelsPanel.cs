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
	private bool _trimLabels;

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
		if (size.Width < availableSize.Width)
		{
			size = size.WithWidth(size.Width - ellipsisDesiredWidth);
			_trimLabels = false;
		}
		else
		{
			_trimLabels = true;
		}

		return InfiniteWidthMeasure ? new Size(double.MaxValue, size.Height) : size;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var spacing = 2.0;
		var ellipsisWidth = 0.0;
		var width = 0.0;
		var height = 0.0;
		var finalWidth = finalSize.Width;
		var showEllipsis = false;
		var totalChildren = Children.Count;
		var count = 0;

		if (EllipsisControl is { })
		{
			ellipsisWidth = EllipsisControl.DesiredSize.Width;
		}

		for (var i = 0; i < totalChildren; i++)
		{
			var child = Children[i];
			var childWidth = child.DesiredSize.Width;

			height = Math.Max(height, child.DesiredSize.Height);

			if (width + childWidth > finalWidth && _trimLabels)
			{
				while (true)
				{
					if (width + ellipsisWidth > finalWidth)
					{
						var previous = i - 1;
						if (previous >= 0)
						{
							var previousChild = Children[previous];
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

				showEllipsis = true;
				if (EllipsisControl is { })
				{
					width += EllipsisControl.DesiredSize.Width;
				}

				break;
			}

			width += child.DesiredSize.Width + spacing;
			count++;
		}

		var offset = 0.0;

		for (var i = 0; i < totalChildren; i++)
		{
			var child = Children[i];
			if (i < count)
			{
				var rect = new Rect(offset, 0.0, child.DesiredSize.Width, height);
				child.Arrange(rect);
				offset += child.DesiredSize.Width + spacing;
			}
			else
			{
				child.Arrange(new Rect(-10000, -10000, 0, 0));
			}
		}

		if (EllipsisControl is { })
		{
			if (showEllipsis)
			{
				var rect = new Rect(offset, 0.0, EllipsisControl.DesiredSize.Width, height);
				EllipsisControl.Arrange(rect);
			}
			else
			{
				EllipsisControl.Arrange(new Rect(-10000, -10000, 0, 0));
			}
		}

		UpdateFilteredItems(count);

		return new Size(width, height);
	}
}
