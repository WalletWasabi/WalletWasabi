using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls.Spectrum;
#pragma warning disable CS0612

public class SpectrumControl : TemplatedControl, ICustomDrawOperation
{
	private const int NumBins = 64;

	private readonly AuraSpectrumDataSource _auraSpectrumDataSource;
	private readonly SplashEffectDataSource _splashEffectDataSource;

	private readonly SpectrumDataSource[] _sources;

	private IBrush? _lineBrush;

	private float[] _data;

	private bool _isAuraActive;
	private bool _isSplashActive;

	private readonly SKPaint _blur = new()
	{
		ImageFilter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp),
		FilterQuality = SKFilterQuality.Low
	};

	private SKColor _lineColor;
	private SKSurface? _surface;
	private readonly DispatcherTimer _invalidationTimer;
	private const double TextureHeight = 32;
	private const double TextureWidth = 32;

	public static readonly StyledProperty<bool> IsActiveProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsActive));

	public static readonly StyledProperty<bool> IsDockEffectVisibleProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsDockEffectVisible));

	public SpectrumControl()
	{
		SetVisibility();
		_data = new float[NumBins];
		_auraSpectrumDataSource = new AuraSpectrumDataSource(NumBins);
		_splashEffectDataSource = new SplashEffectDataSource(NumBins);

		_auraSpectrumDataSource.GeneratingDataStateChanged += OnAuraGeneratingDataStateChanged;
		_splashEffectDataSource.GeneratingDataStateChanged += OnSplashGeneratingDataStateChanged;

		_sources = new SpectrumDataSource[] { _auraSpectrumDataSource, _splashEffectDataSource };

		Background = new RadialGradientBrush()
		{
			GradientStops =
			{
				new GradientStop { Color = Color.Parse("#00000D21"), Offset = 0 },
				new GradientStop { Color = Color.Parse("#FF000D21"), Offset = 1 }
			}
		};

		_invalidationTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(1000.0 / 15.0)
		};

		_invalidationTimer.Tick += (sender, args) => InvalidateVisual();
	}

	public bool IsActive
	{
		get => GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	public bool IsDockEffectVisible
	{
		get => GetValue(IsDockEffectVisibleProperty);
		set => SetValue(IsDockEffectVisibleProperty, value);
	}

	private void OnSplashGeneratingDataStateChanged(object? sender, bool e)
	{
		_isSplashActive = e;
		SetVisibility();
	}

	private void OnAuraGeneratingDataStateChanged(object? sender, bool e)
	{
		_isAuraActive = e;
		SetVisibility();
	}

	private void SetVisibility()
	{
		IsVisible = _isSplashActive || _isAuraActive;
	}

	private void OnIsActiveChanged()
	{
		_auraSpectrumDataSource.IsActive = IsActive;

		if (IsActive)
		{
			_auraSpectrumDataSource.Start();
			_invalidationTimer.Start();
		}
		else
		{
			_invalidationTimer.Stop();
		}
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsActiveProperty)
		{
			OnIsActiveChanged();
		}
		else if (change.Property == IsDockEffectVisibleProperty)
		{
			if (change.NewValue.GetValueOrDefault<bool>() && !IsActive)
			{
				_splashEffectDataSource.Start();
			}
		}
		else if (change.Property == ForegroundProperty)
		{
			_lineBrush = Foreground ?? Brushes.Magenta;

			if (_lineBrush is ImmutableSolidColorBrush brush)
			{
				_lineColor = brush.Color.ToSKColor();
			}
		}
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		for (int i = 0; i < NumBins; i++)
		{
			_data[i] = 0;
		}

		foreach (var source in _sources)
		{
			source.Render(ref _data);
		}

		context.Custom(this);
	}

	private void RenderBars(SKCanvas context)
	{
		context.Clear();
		var width = TextureWidth;
		var height = TextureHeight;
		var thickness = width / NumBins;
		var center = (width / 2);

		double x = 0;

		using var linePaint = new SKPaint()
		{
			Color = _lineColor,
			IsAntialias = false,
			Style = SKPaintStyle.Fill
		};

		using var path = new SKPath();

		for (int i = 0; i < NumBins; i++)
		{
			var dCenter = Math.Abs(x - center);
			var multiplier = 1 - (dCenter / center);
			var rect = new SKRect(
				(float) x,
				(float) height,
				(float) (x + thickness),
				(float) (height - multiplier * _data[i] * (height * 0.8)));
			path.AddRect(rect);

			x += thickness;
		}

		context.DrawPath(path, linePaint);
	}

	void IDisposable.Dispose()
	{
		// nothing to do.
	}

	bool IDrawOperation.HitTest(Point p) => Bounds.Contains(p);

	void IDrawOperation.Render(IDrawingContextImpl context)
	{
		var bounds = Bounds;

		if (context is not ISkiaDrawingContextImpl skia)
		{
			return;
		}

		if (_surface is null)
		{
			if (skia.GrContext is { })
			{
				_surface =
					SKSurface.Create(skia.GrContext, false, new SKImageInfo((int)TextureWidth, (int)TextureHeight));
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
			new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height), _blur);
	}

	bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => false;
}
