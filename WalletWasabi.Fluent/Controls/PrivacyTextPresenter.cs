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
	private FormattedText? _formattedText;

	private FormattedText CreateFormattedText()
	{
		return new FormattedText(
			"",
			new Typeface(FontFamily, FontStyle, FontWeight),
			FontSize,
			TextAlignment.Left,
			TextWrapping.NoWrap,
			Size.Infinity);
	}

	private GlyphRun? CreateGlyphRun(double width)
	{
		var privacyChar = UIConstants.PrivacyChar;

		var glyphTypeface = new Typeface((FontFamily?) FontFamily).GlyphTypeface;
		var glyph = glyphTypeface.GetGlyph(privacyChar);

		var scale = FontSize / glyphTypeface.DesignEmHeight;
		var advance = glyphTypeface.GetGlyphAdvance(glyph) * scale;

		var count = width > 0 && width < advance ? 1 : (int)(width / advance);
		if (count == 0)
		{
			return null;
		}

		var advances = new ReadOnlySlice<double>(new ReadOnlyMemory<double>(Enumerable.Repeat(advance, count).ToArray()));
		var characters = new ReadOnlySlice<char>(new ReadOnlyMemory<char>(Enumerable.Repeat(privacyChar, count).ToArray()));
		var glyphs = new ReadOnlySlice<ushort>(new ReadOnlyMemory<ushort>(Enumerable.Repeat(glyph, count).ToArray()));

		return new GlyphRun(glyphTypeface, FontSize, glyphs, advances, characters: characters);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (_formattedText is null)
		{
			_formattedText = CreateFormattedText();
		}

		return new Size(0, _formattedText.Bounds.Height);
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
