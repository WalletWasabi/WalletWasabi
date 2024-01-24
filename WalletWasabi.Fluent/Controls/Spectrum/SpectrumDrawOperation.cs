using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumDrawOperation : ICustomDrawOperation
{
	private readonly Action<ISkiaSharpApiLease, Rect> _draw;

	public SpectrumDrawOperation(Rect bounds, Action<ISkiaSharpApiLease, Rect> draw)
	{
		_draw = draw;
		Bounds = bounds;
	}

	public Rect Bounds { get; }

	void IDisposable.Dispose()
	{
		// nothing to do.
	}

	bool ICustomDrawOperation.HitTest(Point p) => Bounds.Contains(p);

	bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => false;

	void ICustomDrawOperation.Render(ImmediateDrawingContext context)
	{
		using var skia = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
		if (skia is null)
		{
			return;
		}
		_draw(skia, Bounds);
	}
}
