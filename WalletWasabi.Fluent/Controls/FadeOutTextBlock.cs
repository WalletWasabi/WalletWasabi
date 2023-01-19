using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Controls;

public class FadeOutTextBlock : TextBlock, IStyleable
{
	private TextLayout? _trimmedLayout;
	private Size _constraint;
	private bool _cutOff;

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

	public override void Render(DrawingContext context)
	{
		var background = Background;

		var bounds = Bounds;

		if (background != null)
		{
			context.FillRectangle(background, Bounds);
		}

		if (_trimmedLayout is null)
		{
			return;
		}

		var width = bounds.Size.Width;

		var centerOffset = TextAlignment switch
		{
			TextAlignment.Center => (width - _trimmedLayout.MaxWidth) / 2.0,
			TextAlignment.Right => width - _trimmedLayout.MaxWidth,
			_ => 0.0
		};

		var (left, yPosition, _, _) = Padding;

		using var a =
			context.PushPostTransform(Matrix.CreateTranslation(left + centerOffset, yPosition));
		using var b = _cutOff ? context.PushOpacityMask(FadeoutOpacityMask, Bounds) : Disposable.Empty;
		base.Render(context);
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

		return (_trimmedLayout?.Bounds.Size ?? Size.Empty).Inflate(padding);
	}
}
