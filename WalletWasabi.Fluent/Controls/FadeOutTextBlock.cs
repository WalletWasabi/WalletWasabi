using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class FadeOutTextBlock : TextBlock, IStyleable
{
	public Type StyleKey { get; } = typeof(TextBlock);

	public FadeOutTextBlock()
	{
		TextWrapping = TextWrapping.NoWrap;
	}

	private static IBrush FadeoutOpacityMask = new LinearGradientBrush
	{
		StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
		EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
		GradientStops =
		{
			new GradientStop {Color = Colors.White, Offset = 0},
			new GradientStop {Color = Colors.White, Offset = 0.5},
			new GradientStop {Color = Colors.Transparent, Offset = 0.8}
		}
	}.ToImmutable();

	private TextLayout? _trimmedLayout;
	private Size _constraint;
	private bool _cutOff;
	private TextLayout? _noTrimLayout;

	public override void Render(DrawingContext context)
	{
		var background = Background;

		Rect bounds;

		if (background != null)
		{
			var drawingContext = context;
			var brush = background;
			bounds = Bounds;
			var rect = new Rect(bounds.Size);
			drawingContext.FillRectangle(brush, rect);
		}

		if (_trimmedLayout is null || _noTrimLayout is null)
		{
			return;
		}

		var textAlignment = TextAlignment;
		bounds = Bounds;
		var width = bounds.Size.Width;
		var num1 = 0.0;
		switch (textAlignment)
		{
			case TextAlignment.Center:
				num1 = (width - _trimmedLayout.Size.Width) / 2.0;
				break;
			case TextAlignment.Right:
				num1 = width - _trimmedLayout.Size.Width;
				break;
		}

		var padding = Padding;
		var yPosition = padding.Top;
		var size = _trimmedLayout.Size;
		bounds = Bounds;
		if (bounds.Height < size.Height)
		{
			switch (VerticalAlignment)
			{
				case VerticalAlignment.Center:
					var num2 = yPosition;
					bounds = Bounds;
					var num3 = (bounds.Height - size.Height) / 2.0;
					yPosition = num2 + num3;
					break;
				case VerticalAlignment.Bottom:
					var num4 = yPosition;
					bounds = Bounds;
					var num5 = bounds.Height - size.Height;
					yPosition = num4 + num5;
					break;
			}
		}

		using var a =
			context.PushPostTransform(Matrix.CreateTranslation(padding.Left + num1, yPosition));
		using var b = _cutOff ? context.PushOpacityMask(FadeoutOpacityMask, Bounds) : Disposable.Empty;
		_noTrimLayout.Draw(context);
	}

	private void NewCreateTextLayout(Size constraint, string? text)
	{
		if (constraint == Size.Empty)
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

		_noTrimLayout = new TextLayout(text1, typeface, fontSize, foreground, textAlignment,
			textWrapping, TextTrimming.None, textDecorations, width, height, lineHeight,
			1);

		_trimmedLayout = new TextLayout(text1, typeface, fontSize, foreground, textAlignment,
			textWrapping, TextTrimming.CharacterEllipsis, textDecorations, width, height, lineHeight,
			1);

		_cutOff = _trimmedLayout.TextLines[0].HasCollapsed;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrEmpty(Text))
		{
			return new Size();
		}

		var padding = Padding;
		availableSize = availableSize.Deflate(padding);
		if (_constraint != availableSize)
		{
			_constraint = availableSize;
			NewCreateTextLayout(_constraint, Text);
		}

		var textLayout = _trimmedLayout;
		return (textLayout?.Size ?? Size.Empty).Inflate(padding);
	}
}