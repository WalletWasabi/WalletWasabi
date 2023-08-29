using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Utilities;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Controls;

public class PrivacyTextPresenter : UserControl
{
	private GlyphRun? _glyphRun;
	private double _width;
	private FormattedText _formattedText;

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
			MaxTextHeight = Size.Infinity.Height, MaxTextWidth = Size.Infinity.Width
		};
	}

	private GlyphRun? CreateGlyphRun(double width)
	{
		var privacyChar = UiConstants.PrivacyChar;

		var glyphTypeface = new Typeface((FontFamily?)FontFamily).GlyphTypeface;
		var glyph = glyphTypeface.GetGlyph(privacyChar);

		var scale = FontSize / glyphTypeface.Metrics.DesignEmHeight;
		var advance = glyphTypeface.GetGlyphAdvance(glyph) * scale;

		var count = width > 0 && width < advance ? 1 : (int)(width / advance);
		if (count == 0)
		{
			return null;
		}

		var characters = new ReadOnlyMemory<char>(Enumerable.Repeat(privacyChar, count).ToArray());
		var glyphs = Enumerable.Repeat(glyph, count).ToArray();

		return new GlyphRun(glyphTypeface, FontSize, characters, glyphs);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		_formattedText ??= CreateFormattedText();

		return new Size(0, _formattedText.Height);
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
			(_glyphRun as IDisposable)?.Dispose();
			_glyphRun = CreateGlyphRun(width);
			_width = width;
		}

		if (_glyphRun is { })
		{
			context.DrawGlyphRun(Foreground, _glyphRun);
		}
	}
}
