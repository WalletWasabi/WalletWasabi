using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ReactiveUI;

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
			new GradientStop {Color = Colors.White, Offset = 0.7},
			new GradientStop {Color = Colors.Transparent, Offset = 1}
		}
	}.ToImmutable();

	public override void Render(DrawingContext context)
	{
		var background = Background;

		if (background != null)
		{
			context.FillRectangle(background, new Rect(Bounds.Size));
		}

		if (TextLayout == null)
		{
			return;
		}

		var textWidth = TextLayout.Size.Width;
		var constraints = Bounds.Deflate(Padding);
		var constraintsWidth = constraints.Width;
		var isConstrained = textWidth >= constraintsWidth;

		if (isConstrained)
		{
			using var sd = context.PushOpacityMask(FadeoutOpacityMask, constraints);
			TextLayout.Draw(context);
		}
		else
		{
			TextLayout.Draw(context);
		}
	}
}