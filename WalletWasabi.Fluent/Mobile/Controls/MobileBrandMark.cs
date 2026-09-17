using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>Filled, scalable three-leaf mark for native mobile surfaces.</summary>
public sealed class MobileBrandMark : Control
{
	public static readonly StyledProperty<IBrush?> ForegroundProperty =
		AvaloniaProperty.Register<MobileBrandMark, IBrush?>(nameof(Foreground));
	private static readonly Geometry Left = Geometry.Parse("M23,48 C9,45 2,30 2,10 C16,18 19,32 23,48 Z");
	private static readonly Geometry Middle = Geometry.Parse("M23,48 C13,30 17,10 29,2 C30,17 25,31 23,48 Z");
	private static readonly Geometry Right = Geometry.Parse("M23,48 C23,22 32,9 46,3 C44,26 37,42 23,48 Z");
	static MobileBrandMark() => AffectsRender<MobileBrandMark>(ForegroundProperty);
	public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
	protected override Size MeasureOverride(Size availableSize) => new(28, 28);
	public override void Render(DrawingContext context)
	{
		var side = Math.Min(Bounds.Width, Bounds.Height);
		if (!double.IsFinite(side) || side <= 0 || Foreground is null) return;
		using (context.PushTransform(Matrix.CreateScale(side / 50, side / 50) *
			Matrix.CreateTranslation((Bounds.Width - side) / 2, (Bounds.Height - side) / 2)))
		{
			using (context.PushOpacity(0.72)) context.DrawGeometry(Foreground, null, Left);
			using (context.PushOpacity(0.52)) context.DrawGeometry(Foreground, null, Middle);
			context.DrawGeometry(Foreground, null, Right);
		}
	}
}
