using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumControlState
{
	private const int NumBins = 64;
	private const double TextureHeight = 32;
	private const double TextureWidth = 32;
	private const double Fps = 15.0;

	private SKColor _pathColor;
	private SKSurface? _surface;
	private readonly SpectrumDataSource[] _sources;
	private readonly SKPaint _blur = new()
	{
		ImageFilter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp),
		FilterQuality = SKFilterQuality.Low
	};
	private float[] _data;
	private bool _isGenerating;
	private readonly DispatcherTimer _invalidationTimer;
	private readonly SpectrumControl _control;

	public SpectrumControlState(SpectrumControl control)
	{
		_control = control;
		_data = new float[NumBins];

		AuraSpectrumDataSource = new AuraSpectrumDataSource(NumBins);
		SplashEffectDataSource = new SplashEffectDataSource(NumBins);

		AuraSpectrumDataSource.GeneratingDataStateChanged += OnGeneratingDataStateChanged;
		SplashEffectDataSource.GeneratingDataStateChanged += OnGeneratingDataStateChanged;

		_sources = new SpectrumDataSource[] { AuraSpectrumDataSource, SplashEffectDataSource };

		_invalidationTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(1000.0 / Fps)
		};

		_invalidationTimer.Tick += (_, _) => _control.InvalidateVisual();
	}

	public AuraSpectrumDataSource AuraSpectrumDataSource { get; }

	public SplashEffectDataSource SplashEffectDataSource { get; }

	private void OnGeneratingDataStateChanged(object? sender, EventArgs e)
	{
		_isGenerating = AuraSpectrumDataSource.IsGenerating || SplashEffectDataSource.IsGenerating;

		if (_isGenerating)
		{
			_invalidationTimer.Start();
		}
	}

	public void OnForegroundChanged(SKColor color)
	{
		_pathColor = color;
	}

	public void OnIsActiveChanged()
	{
		if (_control.IsActive)
		{
			AuraSpectrumDataSource.Start();
		}
		else
		{
			AuraSpectrumDataSource.Stop();
		}
	}

	public void Render(DrawingContext context)
	{
		for (int i = 0; i < NumBins; i++)
		{
			_data[i] = 0;
		}

		foreach (var source in _sources)
		{
			source.Render(ref _data);
		}

		// Even if the data generation is finished, let's wait until the animation finishes to disappear.
		// Only stop the rendering once it fully disappeared. (== there is nothing to render)
		if (!_isGenerating && _data.All(f => f <= 0))
		{
			_invalidationTimer.Stop();
		}

		var custom = new SpectrumDrawOperation(_control.Bounds, Draw);

		context.Custom(custom);
	}

	private void Draw(ImmediateDrawingContext context, Rect bounds)
	{
		using var skia = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
		if (skia == null)
		{
			return;
		}

		if (_surface is null)
		{
			if (skia.GrContext is not null)
			{
				_surface =
					SKSurface.Create(skia.GrContext, false,
						new SKImageInfo((int)TextureWidth, (int)TextureHeight));
			}
			else
			{
				_surface = SKSurface.Create(
					new SKImageInfo(
						(int)Math.Ceiling(TextureWidth),
						(int)Math.Ceiling(TextureHeight),
						SKImageInfo.PlatformColorType,
						SKAlphaType.Premul));
			}
		}

		RenderBars(_surface.Canvas);

		using var snapshot = _surface.Snapshot();

		skia.SkCanvas.DrawImage(
			snapshot,
			new SKRect(0, 0, (float)TextureWidth, (float)TextureHeight),
			new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height),
			_blur);
	}

	private void RenderBars(SKCanvas context)
	{
		context.Clear();

		var width = TextureWidth;
		var height = TextureHeight;
		var thickness = width / NumBins;
		var center = (width / 2);

		double x = 0;

		using var pathPaint = new SKPaint()
		{
			Color = _pathColor,
			IsAntialias = false,
			Style = SKPaintStyle.Fill
		};

		using var path = new SKPath();

		for (int i = 0; i < NumBins; i++)
		{
			var dCenter = Math.Abs(x - center);
			var multiplier = 1 - (dCenter / center);
			var rect = new SKRect(
				(float)x,
				(float)height,
				(float)(x + thickness),
				(float)(height - multiplier * _data[i] * (height * 0.8)));
			path.AddRect(rect);

			x += thickness;
		}

		context.DrawPath(path, pathPaint);
	}
}
