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
	protected override Size MeasureOverride(Size availableSize)
	{
		var formattedText = new FormattedText(
				"#",
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize,
				TextAlignment.Left,
				TextWrapping.NoWrap,
				availableSize);

		return new Size(FontSize, formattedText.Bounds.Height);
	}

	public override void Render(DrawingContext context)
	{
		if (double.IsNaN(Bounds.Width) || Bounds.Width == 0)
		{
			return;
		}

		var privacyChar = UIConstants.PrivacyChar;

		var glyphTypeface = new Typeface((FontFamily?)FontFamily).GlyphTypeface;
		var glyph = glyphTypeface.GetGlyph(privacyChar);

		var scale = FontSize / glyphTypeface.DesignEmHeight;
		var advance = glyphTypeface.GetGlyphAdvance(glyph) * scale;

		var count = (int)(Bounds.Width / advance);

		var advances = new ReadOnlySlice<double>(new ReadOnlyMemory<double>(Enumerable.Repeat(advance, count).ToArray()));
		var characters = new ReadOnlySlice<char>(new ReadOnlyMemory<char>(Enumerable.Repeat(privacyChar, count).ToArray()));
		var glyphs = new ReadOnlySlice<ushort>(new ReadOnlyMemory<ushort>(Enumerable.Repeat(glyph, count).ToArray()));

		var glyphRun = new GlyphRun(glyphTypeface, FontSize, glyphs, advances, characters: characters);

		context.DrawGlyphRun(Foreground, glyphRun);
	}
}
