using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class FadeOutTextBlock : TextBlock
{
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

	private static readonly IBrush OpacityMask = new LinearGradientBrush
	{
		StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
		EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
		GradientStops =
		{
			new GradientStop { Color = Colors.White, Offset = 0 },
			new GradientStop { Color = Colors.White, Offset = 1 },
		}
	}.ToImmutable();

	internal TextBlock? TrimmedTextBlock { get; set; }

	protected override void RenderTextLayout(DrawingContext context, Point origin)
	{
		if (TrimmedTextBlock is null)
		{
			base.RenderTextLayout(context, origin);
		}
		else
		{
			var hasCollapsed = TrimmedTextBlock.TextLayout.TextLines[0].HasCollapsed;
			if (hasCollapsed)
			{
				using var _ = context.PushOpacityMask(FadeoutOpacityMask, Bounds);
				TextLayout.Draw(context, origin + new Point(TextLayout.OverhangLeading, 0));
			}
			else
			{
				using var _ = context.PushOpacityMask(OpacityMask, Bounds);
				TextLayout.Draw(context, origin + new Point(TextLayout.OverhangLeading, 0));
			}
		}
	}
}
