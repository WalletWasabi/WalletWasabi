using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using System.Reactive.Disposables;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls;

public class FadeOutTextBlock : TextBlock
{
	private TextLayout? _trimmedLayout;
 	private bool _cutOff;
    private TextLayout? _noTrimLayout;
    private Size _constraint;

	public FadeOutTextBlock()
	{
		TextWrapping = TextWrapping.NoWrap;
	}

	protected override Type StyleKeyOverride => typeof(TextBlock);

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
		var bounds = Bounds;

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
		using var b = _cutOff ? context.PushOpacityMask(FadeoutOpacityMask, bounds) : Disposable.Empty;
		_noTrimLayout.Draw(context, origin + new Point(_noTrimLayout.OverhangLeading, 0));
	}
	private readonly record struct SimpleTextSource : ITextSource
	{
		private readonly string _text;
		private readonly TextRunProperties _defaultProperties;

		public SimpleTextSource(string text, TextRunProperties defaultProperties)
		{
			_text = text;
			_defaultProperties = defaultProperties;
		}

		public TextRun? GetTextRun(int textSourceIndex)
		{
			if (textSourceIndex > _text.Length)
			{
				return new TextEndOfParagraph();
			}

			var runText = _text.AsMemory(textSourceIndex);

			if (runText.IsEmpty)
			{
				return new TextEndOfParagraph();
			}

			return new TextCharacters(runText, _defaultProperties);
		}
	}
	private void NewCreateTextLayout(Size constraint, string? text)
	{
		if (constraint == default)
		{
			_trimmedLayout = null;
		}

		var text1 = text ?? "";

		var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

		var defaultProperties = new GenericTextRunProperties(
			typeface,
			FontSize,
			TextDecorations,
			Foreground);

		var paragraphProperties = new GenericTextParagraphProperties(FlowDirection, TextAlignment, true, false,
			defaultProperties, TextWrapping, LineHeight, 0, LetterSpacing);

		var textSource = new SimpleTextSource(text1 ?? "", defaultProperties);

		_noTrimLayout = new TextLayout(
			textSource,
			paragraphProperties,
			TextTrimming.None,
			_constraint.Width,
			_constraint.Height,
			MaxLines);;

		_trimmedLayout = new TextLayout(
			textSource,
			paragraphProperties,
			TextTrimming.CharacterEllipsis,
			_constraint.Width,
			_constraint.Height,
			MaxLines);;

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

		var sizeNoTrim = _noTrimLayout is null ? default : new Size(_noTrimLayout.Width, _noTrimLayout.Height);

		if (availableSize != sizeNoTrim)
		{
			_constraint = availableSize;
			NewCreateTextLayout(_constraint, Text);
		}

		var sizeTrimmed = _trimmedLayout is null ? default : new Size(_trimmedLayout.Width, _trimmedLayout.Height);

		OpacityMask = _cutOff ? FadeoutOpacityMask : Brushes.White;

		return sizeTrimmed.Inflate(padding);
	}
}
