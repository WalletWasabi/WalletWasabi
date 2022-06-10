using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Controls;

public class Overlay : Control
{
	public static readonly StyledProperty<bool> EnabledProperty =
		AvaloniaProperty.Register<Overlay, bool>(nameof(Enabled));

	public static readonly StyledProperty<bool> ShowBoundsRectsProperty =
		AvaloniaProperty.Register<Overlay, bool>(nameof(ShowBoundsRects), true);

	public static readonly StyledProperty<bool> ShowHorizontalLinesProperty =
		AvaloniaProperty.Register<Overlay, bool>(nameof(ShowHorizontalLines));

	public static readonly StyledProperty<bool> ShowVerticalLinesProperty =
		AvaloniaProperty.Register<Overlay, bool>(nameof(ShowVerticalLines));

	public bool Enabled
	{
		get => GetValue(EnabledProperty);
		set => SetValue(EnabledProperty, value);
	}

	public bool ShowBoundsRects
	{
		get => GetValue(ShowBoundsRectsProperty);
		set => SetValue(ShowBoundsRectsProperty, value);
	}

	public bool ShowHorizontalLines
	{
		get => GetValue(ShowHorizontalLinesProperty);
		set => SetValue(ShowHorizontalLinesProperty, value);
	}

	public bool ShowVerticalLines
	{
		get => GetValue(ShowVerticalLinesProperty);
		set => SetValue(ShowVerticalLinesProperty, value);
	}
#if DEBUG
	private List<ILogical>? _descendants;

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsEnabledProperty)
		{
			var isEnabled = change.NewValue.GetValueOrDefault<bool>();

			UpdateDescendants(isEnabled);
			InvalidateVisual();
		}

		if (change.Property == ShowBoundsRectsProperty
		    || change.Property == ShowHorizontalLinesProperty
		    || change.Property == ShowVerticalLinesProperty)
		{
			InvalidateVisual();
		}
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (this.GetVisualRoot() is TopLevel root)
		{
			root.Renderer.SceneInvalidated += RendererOnSceneInvalidated;
			// root.LayoutUpdated += OnLayoutUpdated;
			root.KeyDown += OnKeyDown;
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);

		if (this.GetVisualRoot() is TopLevel root)
		{
			root.Renderer.SceneInvalidated -= RendererOnSceneInvalidated;
			// root.LayoutUpdated -= OnLayoutUpdated;
			root.KeyDown -= OnKeyDown;
		}
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.F7)
		{
			ShowBoundsRects = !ShowBoundsRects;
		}

		if (e.Key == Key.F8)
		{
			ShowHorizontalLines = !ShowHorizontalLines;
		}

		if (e.Key == Key.F9)
		{
			ShowVerticalLines = !ShowVerticalLines;
		}

		if (e.Key == Key.F10)
		{
			IsEnabled = !IsEnabled;
		}
	}

	private void RendererOnSceneInvalidated(object? sender, SceneInvalidatedEventArgs e)
	{
		if (IsEnabled)
		{
			UpdateDescendants(IsEnabled);
			InvalidateVisual();
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

		var penBounds = new ImmutablePen(Colors.Red.ToUint32());
		var penLines = new ImmutablePen(Colors.Cyan.ToUint32());

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

			var tl = rect.TopLeft.Transform(tb.Transform);
			var br = rect.TopLeft.Transform(tb.Transform);

			if (ShowHorizontalLines)
			{
				context.DrawLine(penLines, new Point(0, tl.Y + 0.5), new Point(Bounds.Width, tl.Y + 0.5));
				context.DrawLine(penLines, new Point(0, br.X + 0.5), new Point(Bounds.Width, br.X + 0.5));
			}

			if (ShowVerticalLines)
			{
				context.DrawLine(penLines, new Point(tl.X + 0.5, 0), new Point(tl.X + 0.5, Bounds.Height));
				context.DrawLine(penLines, new Point(br.X + 0.5, 0), new Point(br.X + 0.5, Bounds.Height));
			}

			if (ShowBoundsRects)
			{
				using var _ = context.PushSetTransform(tb.Transform);
				context.DrawRectangle(null, penBounds, rect);
			}
		}
	}
#endif
}