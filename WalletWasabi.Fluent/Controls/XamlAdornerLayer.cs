using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public class XamlAdornerLayer : AvaloniaObject
{
	/// <summary>
	/// Allows for getting and setting of the adorner for control.
	/// </summary>
	public static readonly AttachedProperty<Control?> AdornerProperty =
		AvaloniaProperty.RegisterAttached<AdornerLayer, Visual, Control?>("Adorner");

	private static readonly AttachedProperty<AdornerLayer?> SavedAdornerLayerProperty =
		AvaloniaProperty.RegisterAttached<Visual, Visual, AdornerLayer?>("SavedAdornerLayer");

	static XamlAdornerLayer()
	{
		AdornerProperty.Changed.Subscribe(AdornerChanged);
	}

	public static Control? GetAdorner(Visual visual)
	{
		return visual.GetValue(AdornerProperty);
	}

	public static void SetAdorner(Visual visual, Control? adorner)
	{
		visual.SetValue(AdornerProperty, adorner);
	}

	private static void AdornerChanged(AvaloniaPropertyChangedEventArgs<Control?> e)
	{
		if (e.Sender is Visual visual)
		{
			var oldAdorner = e.OldValue.GetValueOrDefault();
			var newAdorner = e.NewValue.GetValueOrDefault();

			if (Equals(oldAdorner, newAdorner))
			{
				return;
			}

			if (oldAdorner is { })
			{
				visual.AttachedToVisualTree -= VisualOnAttachedToVisualTree;
				visual.DetachedFromVisualTree -= VisualOnDetachedFromVisualTree;
				Detach(visual, oldAdorner);
			}

			if (newAdorner is { })
			{
				visual.AttachedToVisualTree += VisualOnAttachedToVisualTree;
				visual.DetachedFromVisualTree += VisualOnDetachedFromVisualTree;
				Attach(visual, newAdorner);
			}
		}
	}

	private static void VisualOnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
	{
		if (sender is Visual visual)
		{
			var adorner = GetAdorner(visual);
			if (adorner is { })
			{
				Attach(visual, adorner);
			}
		}
	}

	private static void VisualOnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
	{
		if (sender is Visual visual)
		{
			var adorner = GetAdorner(visual);
			if (adorner is { })
			{
				Detach(visual, adorner);
			}
		}
	}

	private static void Attach(Visual visual, Control adorner)
	{
		var layer = AdornerLayer.GetAdornerLayer(visual);
		AddVisualAdorner(visual, adorner, layer);
		visual.SetValue(SavedAdornerLayerProperty, layer);
	}

	private static void Detach(Visual visual, Control adorner)
	{
		var layer = visual.GetValue(SavedAdornerLayerProperty);
		RemoveVisualAdorner(adorner, layer);
		visual.ClearValue(SavedAdornerLayerProperty);
	}

	private static void AddVisualAdorner(Visual visual, Control? adorner, AdornerLayer? layer)
	{
		if (adorner is null || layer == null || layer.Children.Contains(adorner))
		{
			return;
		}

		AdornerLayer.SetAdornedElement(adorner, visual);
		AdornerLayer.SetIsClipEnabled(adorner, false);

		((ISetLogicalParent)adorner).SetParent(visual);
		layer.Children.Add(adorner);
	}

	private static void RemoveVisualAdorner(Control? adorner, AdornerLayer? layer)
	{
		if (adorner is null || layer is null || !layer.Children.Contains(adorner))
		{
			return;
		}

		layer.Children.Remove(adorner);
		((ISetLogicalParent)adorner).SetParent(null);
	}
}
