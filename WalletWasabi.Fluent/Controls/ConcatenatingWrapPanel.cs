using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Utilities;

namespace WalletWasabi.Fluent.Controls;

/// <summary>
/// Works like a wrap panel.. but concatenates the Items in ConcatenatedChildren.
/// Also the very last child in ConcatenatedChildren will fill the remaining space.
/// </summary>
public class ConcatenatingWrapPanel : Panel, INavigableContainer
{
	/// <summary>
	/// Defines the <see cref="Orientation"/> property.
	/// </summary>
	public static readonly StyledProperty<Orientation> OrientationProperty =
		AvaloniaProperty.Register<ConcatenatingWrapPanel, Orientation>(nameof(Orientation), defaultValue: Orientation.Horizontal);

	/// <summary>
	/// Defines the <see cref="ItemWidth"/> property.
	/// </summary>
	public static readonly StyledProperty<double> ItemWidthProperty =
		AvaloniaProperty.Register<ConcatenatingWrapPanel, double>(nameof(ItemWidth), double.NaN);

	/// <summary>
	/// Defines the <see cref="ItemHeight"/> property.
	/// </summary>
	public static readonly StyledProperty<double> ItemHeightProperty =
		AvaloniaProperty.Register<ConcatenatingWrapPanel, double>(nameof(ItemHeight), double.NaN);

	/// <summary>
	/// Initializes static members of the <see cref="ConcatenatingWrapPanel"/> class.
	/// </summary>
	static ConcatenatingWrapPanel()
	{
		AffectsMeasure<ConcatenatingWrapPanel>(OrientationProperty, ItemWidthProperty, ItemHeightProperty);
	}

	public ConcatenatingWrapPanel()
	{
		ConcatenatedChildren.CollectionChanged += ChildrenChanged;
	}

	public Avalonia.Controls.Controls ConcatenatedChildren { get; } = new Avalonia.Controls.Controls();

	/// <summary>
	/// Gets or sets the orientation in which child controls will be layed out.
	/// </summary>
	public Orientation Orientation
	{
		get => GetValue(OrientationProperty);
		set => SetValue(OrientationProperty, value);
	}

	/// <summary>
	/// Gets or sets the width of all items in the WrapPanel.
	/// </summary>
	public double ItemWidth
	{
		get => GetValue(ItemWidthProperty);
		set => SetValue(ItemWidthProperty, value);
	}

	/// <summary>
	/// Gets or sets the height of all items in the WrapPanel.
	/// </summary>
	public double ItemHeight
	{
		get => GetValue(ItemHeightProperty);
		set => SetValue(ItemHeightProperty, value);
	}

	/// <summary>
	/// Gets the next control in the specified direction.
	/// </summary>
	/// <param name="direction">The movement direction.</param>
	/// <param name="from">The control from which movement begins.</param>
	/// <param name="wrap">Whether to wrap around when the first or last item is reached.</param>
	/// <returns>The control.</returns>
	IInputElement INavigableContainer.GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
	{
		var orientation = Orientation;
		var children = Children.Concat(ConcatenatedChildren).ToList();
		bool horiz = orientation == Orientation.Horizontal;
		int index = Children.IndexOf((Control)from);

		switch (direction)
		{
			case NavigationDirection.First:
				index = 0;
				break;

			case NavigationDirection.Last:
				index = children.Count - 1;
				break;

			case NavigationDirection.Next:
				++index;
				break;

			case NavigationDirection.Previous:
				--index;
				break;

			case NavigationDirection.Left:
				index = horiz ? index - 1 : -1;
				break;

			case NavigationDirection.Right:
				index = horiz ? index + 1 : -1;
				break;

			case NavigationDirection.Up:
				index = horiz ? -1 : index - 1;
				break;

			case NavigationDirection.Down:
				index = horiz ? -1 : index + 1;
				break;
		}

		if (index >= 0 && index < children.Count)
		{
			return children[index];
		}
		else
		{
			return null!;
		}
	}

	/// <inheritdoc/>
	protected override Size MeasureOverride(Size constraint)
	{
		double itemWidth = ItemWidth;
		double itemHeight = ItemHeight;
		var orientation = Orientation;
		var children = Children.Concat(ConcatenatedChildren).ToList();
		var curLineSize = new UVSize(orientation);
		var panelSize = new UVSize(orientation);
		var uvConstraint = new UVSize(orientation, constraint.Width, constraint.Height);
		bool itemWidthSet = !double.IsNaN(itemWidth);
		bool itemHeightSet = !double.IsNaN(itemHeight);

		var childConstraint = new Size(
			itemWidthSet ? itemWidth : constraint.Width,
			itemHeightSet ? itemHeight : constraint.Height);

		for (int i = 0, count = children.Count; i < count; i++)
		{
			var child = children[i];
			if (child is not null)
			{
				// Flow passes its own constraint to children
				child.Measure(childConstraint);

				// This is the size of the child in UV space
				var sz = new UVSize(
					orientation,
					itemWidthSet ? itemWidth : child.DesiredSize.Width,
					itemHeightSet ? itemHeight : child.DesiredSize.Height);

				if (MathUtilities.GreaterThan(curLineSize.U + sz.U, uvConstraint.U)) // Need to switch to another line
				{
					panelSize.U = Math.Max(curLineSize.U, panelSize.U);
					panelSize.V += curLineSize.V;
					curLineSize = sz;

					if (MathUtilities.GreaterThan(sz.U, uvConstraint.U)) // The element is wider then the constraint - give it a separate line
					{
						panelSize.U = Math.Max(sz.U, panelSize.U);
						panelSize.V += sz.V;
						curLineSize = new UVSize(orientation);
					}
				}
				else // Continue to accumulate a line
				{
					curLineSize.U += sz.U;
					curLineSize.V = Math.Max(sz.V, curLineSize.V);
				}
			}
		}

		// The last line size, if any should be added
		panelSize.U = Math.Max(curLineSize.U, panelSize.U);
		panelSize.V += curLineSize.V;

		// Go from UV space to W/H space
		return new Size(panelSize.Width, panelSize.Height);
	}

	/// <inheritdoc/>
	protected override Size ArrangeOverride(Size finalSize)
	{
		double itemWidth = ItemWidth;
		double itemHeight = ItemHeight;
		var orientation = Orientation;
		var children = Children.Concat(ConcatenatedChildren).ToList();
		int firstInLine = 0;
		double accumulatedV = 0;
		double itemU = orientation == Orientation.Horizontal ? itemWidth : itemHeight;
		var curLineSize = new UVSize(orientation);
		var uvFinalSize = new UVSize(orientation, finalSize.Width, finalSize.Height);
		bool itemWidthSet = !double.IsNaN(itemWidth);
		bool itemHeightSet = !double.IsNaN(itemHeight);
		bool useItemU = orientation == Orientation.Horizontal ? itemWidthSet : itemHeightSet;
		bool hasWrapped = false;

		for (int i = 0; i < children.Count; i++)
		{
			var child = children[i];
			if (child is not null)
			{
				var sz = new UVSize(
					orientation,
					itemWidthSet ? itemWidth : child.DesiredSize.Width,
					itemHeightSet ? itemHeight : child.DesiredSize.Height);

				if (MathUtilities.GreaterThan(curLineSize.U + sz.U, uvFinalSize.U)) // Need to switch to another line
				{
					hasWrapped = true;
					ArrangeLine(accumulatedV, curLineSize.V, firstInLine, i, useItemU, itemU, uvFinalSize);

					accumulatedV += curLineSize.V;
					curLineSize = sz;

					if (MathUtilities.GreaterThan(sz.U, uvFinalSize.U)) // The element is wider then the constraint - give it a separate line
					{
						// Switch to next line which only contain one element
						ArrangeLine(accumulatedV, sz.V, i, ++i, useItemU, itemU, uvFinalSize);

						accumulatedV += sz.V;
						curLineSize = new UVSize(orientation);
					}

					firstInLine = i;
				}
				else // Continue to accumulate a line
				{
					curLineSize.U += sz.U;
					curLineSize.V = Math.Max(sz.V, curLineSize.V);
				}
			}
		}

		// Arrange the last line, if any
		if (firstInLine < children.Count)
		{
			ArrangeLine(accumulatedV, curLineSize.V, firstInLine, children.Count, useItemU, itemU, uvFinalSize);
		}

		PseudoClasses.Set(":wrapped", hasWrapped);

		return finalSize;
	}

	private void ArrangeLine(double v, double lineV, int start, int end, bool useItemU, double itemU, UVSize uvFinalSize)
	{
		var orientation = Orientation;
		var children = Children.Concat(ConcatenatedChildren).ToList();
		double u = 0;
		bool isHorizontal = orientation == Orientation.Horizontal;

		for (int i = start; i < end; i++)
		{
			var child = children[i];
			if (child is not null)
			{
				if (i == children.Count - 1)
				{
					var childSize = new UVSize(orientation, uvFinalSize.Width - u, uvFinalSize.Height - u);
					double layoutSlotU = useItemU ? itemU : childSize.U;
					child.Arrange(new Rect(
						isHorizontal ? u : v,
						isHorizontal ? v : u,
						isHorizontal ? layoutSlotU : lineV,
						isHorizontal ? lineV : layoutSlotU));
					u += layoutSlotU;
				}
				else
				{
					var childSize = new UVSize(orientation, child.DesiredSize.Width, child.DesiredSize.Height);
					double layoutSlotU = useItemU ? itemU : childSize.U;
					child.Arrange(new Rect(
						isHorizontal ? u : v,
						isHorizontal ? v : u,
						isHorizontal ? layoutSlotU : lineV,
						isHorizontal ? lineV : layoutSlotU));
					u += layoutSlotU;
				}
			}
		}
	}

	private struct UVSize
	{
		internal double U;
		internal double V;
		private Orientation _orientation;

		internal UVSize(Orientation orientation, double width, double height)
		{
			U = V = 0d;
			_orientation = orientation;
			Width = width;
			Height = height;
		}

		internal UVSize(Orientation orientation)
		{
			U = V = 0d;
			_orientation = orientation;
		}

		internal double Width
		{
			get => _orientation == Orientation.Horizontal ? U : V;
			set
			{
				if (_orientation == Orientation.Horizontal)
				{
					U = value;
				}
				else
				{
					V = value;
				}
			}
		}

		internal double Height
		{
			get => _orientation == Orientation.Horizontal ? V : U;
			set
			{
				if (_orientation == Orientation.Horizontal)
				{
					V = value;
				}
				else
				{
					U = value;
				}
			}
		}
	}
}
