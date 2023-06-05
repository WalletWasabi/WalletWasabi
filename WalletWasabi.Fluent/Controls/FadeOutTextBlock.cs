using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using System.Reactive.Disposables;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls;

public class FadeOutTextBlock : TextBlock, IStyleable
{
	private TextLayout? _trimmedLayout;
 	private bool _cutOff;
    private TextLayout? _noTrimLayout;
    private Size _constraint;

	public FadeOutTextBlock()
	{
		TextWrapping = TextWrapping.NoWrap;
	}

	public Type StyleKey { get; } = typeof(TextBlock);

	private static readonly IBrush FadeoutOpacityMask = new LinearGradientBrush
	{
		StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
		EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
		GradientStops =
		{
			new GradientStop { Color = Colors.White, Offset = 0 },
			new GradientStop { Color = Colors.White, Offset = 0.7 },
			new GradientStop { Color = Colors.Transparent, Offset = 0.9 }
		}
	}.ToImmutable();

	protected override void RenderTextLayout(DrawingContext context, Point origin)
	{
		var background = Background;

		var bounds = Bounds;

		if (background != null)
		{
			context.FillRectangle(background, Bounds);
		}

		if (_trimmedLayout is null || _noTrimLayout is null)
		{
			return;
		}

		var width = bounds.Size.Width;

		var centerOffset = TextAlignment switch
		{
			TextAlignment.Center => (width - _trimmedLayout.Width) / 2.0,
			TextAlignment.Right => width - _trimmedLayout.Width,
			_ => 0.0
		};

		var (left, yPosition, _, _) = Padding;

		using var a = context.PushTransform(Matrix.CreateTranslation(left + centerOffset, yPosition));
		using var b = _cutOff ? context.PushOpacityMask(FadeoutOpacityMask, Bounds) : Disposable.Empty;
		_noTrimLayout.Draw(context, origin);
	}

	private void NewCreateTextLayout(Size constraint, string? text)
	{
		if (constraint == default)
		{
			_trimmedLayout = null;
		}

		var text1 = text ?? "";
		var typeface = new Typeface(FontFamily, FontStyle, FontWeight);
		var fontSize = FontSize;
		var foreground = Foreground;
		var textAlignment = TextAlignment;
		var textWrapping = TextWrapping;
		var textDecorations = TextDecorations;
		var width = constraint.Width;
		var height = constraint.Height;
		var lineHeight = LineHeight;

		_noTrimLayout = new TextLayout(
			text1,
			typeface,
			fontSize,
			foreground,
			textAlignment,
			textWrapping,
			TextTrimming.None,
			textDecorations,
			FlowDirection.LeftToRight,
			width,
			height,
			lineHeight,
			1);

		_trimmedLayout = new TextLayout(
			text1,
			typeface,
			fontSize,
			foreground,
			textAlignment,
			textWrapping,
			TextTrimming.CharacterEllipsis,
			textDecorations,
			FlowDirection.LeftToRight,
			width,
			height,
			lineHeight,
			1);

		_cutOff = _trimmedLayout.TextLines[0].HasCollapsed;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var scale = LayoutHelper.GetLayoutScale(this);
		var padding = LayoutHelper.RoundLayoutThickness(Padding, scale, scale);

		_constraint = availableSize.Deflate(padding);

		if (string.IsNullOrEmpty(Text))
		{
			return new Size();
		}

		if (_constraint != availableSize)
		{
			_constraint = availableSize;
			NewCreateTextLayout(_constraint, Text);
		}

		var size = _trimmedLayout is null ? default : new Size(_trimmedLayout.Width, _trimmedLayout.Height);

		OpacityMask = _cutOff ? FadeoutOpacityMask : Brushes.White;

		return size.Inflate(padding);
	}
}
