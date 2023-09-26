using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumDrawOperation : ICustomDrawOperation
{
	private readonly Action<ImmediateDrawingContext, Rect> _draw;

	public SpectrumDrawOperation(Rect bounds, Action<ImmediateDrawingContext, Rect> draw)
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

	void ICustomDrawOperation.Render(ImmediateDrawingContext context) => _draw(context, Bounds);
}
