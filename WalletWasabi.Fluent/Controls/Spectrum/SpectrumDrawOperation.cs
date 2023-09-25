using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumDrawOperation : ICustomDrawOperation
{
	private readonly SpectrumControlState _state;

	public SpectrumDrawOperation(Rect bounds, SpectrumControlState state)
	{
		_state = state;
		Bounds = bounds;
	}

	public Rect Bounds { get; }

	void IDisposable.Dispose()
	{
		// nothing to do.
	}

	bool ICustomDrawOperation.HitTest(Point p) => Bounds.Contains(p);

	void ICustomDrawOperation.Render(ImmediateDrawingContext context)
	{
		var bounds = Bounds;

		var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();

		if (leaseFeature == null)
		{
			return;
		}

		using var skia = leaseFeature.Lease();

		if (_state._surface is null)
		{
			if (skia.GrContext is { })
			{
				_state._surface =
					SKSurface.Create(skia.GrContext, false, new SKImageInfo((int)SpectrumControlState.TextureWidth, (int)SpectrumControlState.TextureHeight));
			}
			else
			{
				_state._surface = SKSurface.Create(
					new SKImageInfo(
						(int)Math.Ceiling(SpectrumControlState.TextureWidth),
						(int)Math.Ceiling(SpectrumControlState.TextureHeight),
						SKImageInfo.PlatformColorType,
						SKAlphaType.Premul));
			}
		}

		RenderBars(_state._surface.Canvas);

		using var snapshot = _state._surface.Snapshot();

		skia.SkCanvas.DrawImage(
			snapshot,
			new SKRect(0, 0, (float)SpectrumControlState.TextureWidth, (float)SpectrumControlState.TextureHeight),
			new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height),
			_state._blur);
	}

	bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => false;

	private void RenderBars(SKCanvas context)
	{
		context.Clear();
		var width = SpectrumControlState.TextureWidth;
		var height = SpectrumControlState.TextureHeight;
		var thickness = width / SpectrumControlState.NumBins;
		var center = (width / 2);

		double x = 0;

		using var linePaint = new SKPaint()
		{
			Color = _state._lineColor,
			IsAntialias = false,
			Style = SKPaintStyle.Fill
		};

		using var path = new SKPath();

		for (int i = 0; i < SpectrumControlState.NumBins; i++)
		{
			var dCenter = Math.Abs(x - center);
			var multiplier = 1 - (dCenter / center);
			var rect = new SKRect(
				(float)x,
				(float)height,
				(float)(x + thickness),
				(float)(height - multiplier * _state._data[i] * (height * 0.8)));
			path.AddRect(rect);

			x += thickness;
		}

		context.DrawPath(path, linePaint);
	}
}
