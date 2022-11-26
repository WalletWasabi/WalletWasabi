using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Utilities;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Controls;

public class PrivacyTextPresenter : UserControl
{
	private GlyphRun? _glyphRun;

	public PrivacyTextPresenter()
	{
		//this.WhenAnyValue(x => x.Bounds, x => x.FontSize, x => x.FontFamily)
		//	.Where(t => t.Item1.Width > 0)
		//	.Subscribe(_ => CreateGlyphRun());
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var formattedText = new FormattedText(
				"#",
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize,
				TextAlignment.Left,
				TextWrapping.NoWrap,
				availableSize);

		return new Size(10, formattedText.Bounds.Height);
	}

	public override void Render(DrawingContext context)
	{
		if (Bounds.Width == double.NaN || Bounds.Width == 0)
		{
			return;
		}

		//var foreground = TextBlock.GetForeground(this);
		var foreground = this.Foreground;

		var privacyChar = UIConstants.PrivacyChar;
		//var fontFamily = TextBlock.GetFontFamily(this);
		//var fontSize = TextBlock.GetFontSize(this);

		var fontFamily = FontFamily;
		var fontSize = FontSize;

		var glyphTypeface = new Typeface(fontFamily).GlyphTypeface;
		var glyph = glyphTypeface.GetGlyph(privacyChar);

		var scale = fontSize / glyphTypeface.DesignEmHeight;
		var advance = glyphTypeface.GetGlyphAdvance(glyph) * scale;

		var count = (int)(Bounds.Width / advance);

		var advances = new ReadOnlySlice<double>(new ReadOnlyMemory<double>(Enumerable.Repeat(advance, count).ToArray()));
		var characters = new ReadOnlySlice<char>(new ReadOnlyMemory<char>(Enumerable.Repeat(privacyChar, count).ToArray()));
		var glyphs = new ReadOnlySlice<ushort>(new ReadOnlyMemory<ushort>(Enumerable.Repeat(glyph, count).ToArray()));

		_glyphRun = new GlyphRun(glyphTypeface, fontSize, glyphs, advances, characters: characters);

		context.DrawGlyphRun(foreground, _glyphRun);
	}
}
