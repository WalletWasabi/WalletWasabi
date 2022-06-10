using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Controls;

public class Overlay : Control
{
	public static readonly StyledProperty<bool> EnabledProperty =
		AvaloniaProperty.Register<Overlay, bool>(nameof(Enabled));

	private List<ILogical>? _descendants;

	public bool Enabled
	{
		get => GetValue(EnabledProperty);
		set => SetValue(EnabledProperty, value);
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsEnabledProperty)
		{
			var isEnabled = change.NewValue.GetValueOrDefault<bool>();

			UpdateDescendants(isEnabled);
			InvalidateVisual();
		}
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (this.GetVisualRoot() is TopLevel root)
		{
			root.LayoutUpdated += OnLayoutUpdated;
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);

		if (this.GetVisualRoot() is TopLevel root)
		{
			root.LayoutUpdated -= OnLayoutUpdated;
		}
	}

	private void OnLayoutUpdated(object? sender, EventArgs e)
	{
		if (IsEnabled)
		{
			UpdateDescendants(IsEnabled);
			InvalidateVisual();
		}
	}

	private IEnumerable<ILogical> GetDescendants(ILogical root)
	{
		foreach (var child in root.LogicalChildren)
		{
			yield return child;

			foreach (var next in GetDescendants(child))
			{
				yield return next;
			}
		}
	}

	private void UpdateDescendants(bool isEnabled)
	{
		if (isEnabled && this.GetVisualRoot() is TopLevel root)
		{
			_descendants = GetDescendants(root).ToList();
		}
		else
		{
			_descendants = null;
		}
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		if (_descendants is null || !IsEnabled)
		{
			return;
		}

		var pen = new ImmutablePen(Colors.Red.ToUint32());

		foreach (var logical in _descendants)
		{
			if (logical is not IVisual visual)
			{
				continue;
			}

			if (!visual.IsVisible)
			{
				continue;
			}

			if (!visual.TransformedBounds.HasValue)
			{
				continue;
			}

			var tb = visual.TransformedBounds.Value;
			var b = tb.Bounds;
			var rect = new Rect(b.Left + 0.5, b.Top + 0.5, b.Width + 0.5, b.Height + 0.5);

			using var _ = context.PushSetTransform(tb.Transform);

			context.DrawRectangle(null, pen, rect);
		}
	}
}