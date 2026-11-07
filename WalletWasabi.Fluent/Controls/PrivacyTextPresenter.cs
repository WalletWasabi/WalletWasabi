using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WalletWasabi.Fluent.Configuration;

namespace WalletWasabi.Fluent.Controls;

public class PrivacyTextPresenter : UserControl
{
	public static readonly StyledProperty<int> MinPrivacyCharsProperty =
		AvaloniaProperty.Register<PrivacyTextPresenter, int>(nameof(MinPrivacyChars));

	public static readonly StyledProperty<int> MaxPrivacyCharsProperty =
		AvaloniaProperty.Register<PrivacyTextPresenter, int>(nameof(MaxPrivacyChars), int.MaxValue);

	private GlyphRun? _glyphRun;
	private double _width;
	private FormattedText? _formattedText;

	public int MinPrivacyChars
	{
		get => GetValue(MinPrivacyCharsProperty);
		set => SetValue(MinPrivacyCharsProperty, value);
	}

	public int MaxPrivacyChars
	{
		get => GetValue(MaxPrivacyCharsProperty);
		set => SetValue(MaxPrivacyCharsProperty, value);
	}

	private FormattedText CreateFormattedText()
	{
		return new FormattedText(
			"X",
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface(FontFamily, FontStyle, FontWeight),
			FontSize,
			null)
		{
			TextAlignment = TextAlignment.Left,
			MaxTextHeight = Size.Infinity.Height,
			MaxTextWidth = Size.Infinity.Width
		};
	}

	private double PrivacyCharAdvance()
	{
		var glyphTypeface = new Typeface(FontFamily).GlyphTypeface;
		var scale = FontSize / glyphTypeface.Metrics.DesignEmHeight;
		return glyphTypeface.GetGlyphAdvance(glyphTypeface.GetGlyph(UiConstants.PrivacyChar)) * scale;
	}

	private GlyphRun? CreateGlyphRun(double width)
	{
		var advance = PrivacyCharAdvance();
		var count = Math.Clamp(width > 0 && width < advance ? 1 : (int)(width / advance), MinPrivacyChars, MaxPrivacyChars);
		if (count == 0)
		{
			return null;
		}

		var glyphTypeface = new Typeface(FontFamily).GlyphTypeface;
		var characters = new ReadOnlyMemory<char>(Enumerable.Repeat(UiConstants.PrivacyChar, count).ToArray());
		var glyphs = Enumerable.Repeat(glyphTypeface.GetGlyph(UiConstants.PrivacyChar), count).ToArray();

		return new GlyphRun(glyphTypeface, FontSize, characters, glyphs);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		_formattedText ??= CreateFormattedText();

		return new Size(MinPrivacyChars * PrivacyCharAdvance(), _formattedText.Height);
	}

	public override void Render(DrawingContext context)
	{
		if (double.IsNaN(Bounds.Width) || Bounds.Width == 0)
		{
			return;
		}

		var width = Bounds.Width;
		if (_glyphRun is null || width != _width)
		{
			_glyphRun?.Dispose();
			_glyphRun = CreateGlyphRun(width);
			_width = width;
		}

		if (_glyphRun is { })
		{
			context.DrawGlyphRun(Foreground, _glyphRun);
		}
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == ForegroundProperty)
		{
			InvalidateVisual();
		}
	}
}
