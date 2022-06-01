using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using Avalonia.Threading;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls.Spectrum;
#pragma warning disable CS0612

public class SpectrumControl : TemplatedControl, ICustomDrawOperation
{
	private const int NumBins = 250;

	private readonly AuraSpectrumDataSource _auraSpectrumDataSource;
	private readonly SplashEffectDataSource _splashEffectDataSource;

	private readonly SpectrumDataSource[] _sources;

	private ImmutablePen? _linePen;
	private IBrush? _lineBrush;
	private SKPaint? _linePaint;
	private double _barThickness;

	private float[] _data;

	private bool _isAuraActive;
	private bool _isSplashActive;

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
				_linePaint?.Dispose();
				_linePaint = new SKPaint()
				{
					Color = brush.Color.ToSKColor(),
					IsAntialias = false,
					Style = SKPaintStyle.Stroke
				};
			}

			InvalidateArrange();
		}
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		_barThickness = finalSize.Width / NumBins;
		_linePen = new ImmutablePen(_lineBrush, finalSize.Width / NumBins);
		if (_linePaint is { })
		{
			_linePaint.StrokeWidth = (float)_barThickness;
		}

		return base.ArrangeOverride(finalSize);
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

		Dispatcher.UIThread.Post(() => InvalidateVisual());
	}

	private void RenderBars(IDrawingContextImpl context)
	{
		var thickness = Bounds.Width / NumBins;
		var center = (Bounds.Width / 2);

		double x = 0;

		for (int i = 0; i < NumBins; i++)
		{
			var dCenter = Math.Abs(x - center);
			var multiplier = 1 - (dCenter / center);

			context.DrawLine(
				_linePen,
				new Point(x, Bounds.Height),
				new Point(x, Bounds.Height - multiplier * _data[i] * (Bounds.Height * 0.8)));

			x += thickness;
		}
	}

	private void RenderBars(SKCanvas context)
	{
		var width = Bounds.Width;
		var height = Bounds.Height;
		var thickness = width / NumBins;
		var center = (width / 2);

		double x = 0;

		for (int i = 0; i < NumBins; i++)
		{
			var dCenter = Math.Abs(x - center);
			var multiplier = 1 - (dCenter / center);

			context.DrawLine(
				new SKPoint((float)x, (float)height),
				new SKPoint((float)x, (float)(height - multiplier * _data[i] * (height * 0.8))),
				_linePaint);

			x += thickness;
		}
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

		//using var barsLayer = DrawingContextHelper.CreateDrawingContext(bounds.Size, new Vector(96, 96), skia.GrContext);
		//RenderBars(barsLayer);

		using var pictureRecorder = new SKPictureRecorder();
		pictureRecorder.BeginRecording(SKRect.Create(0f, 0f, (float) bounds.Size.Width, (float) bounds.Size.Height));
		RenderBars(pictureRecorder.RecordingCanvas);
		//using var filter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp);
		//using var paint = new SKPaint { ImageFilter = filter };
		//pictureRecorder.RecordingCanvas.DrawPaint(paint);
		var picture = pictureRecorder.EndRecording();
		skia.SkCanvas.DrawPicture(picture);

		//using var crop = new SKImageFilter.CropRect(Bounds.ToSKRect());
		//using var filter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp, null, crop);
		//using var paint = new SKPaint { ImageFilter = filter };
		//barsLayer.DrawTo(skia, paint);
		//barsLayer.DrawTo(skia, new SKPaint());
	}

	bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => false;
}
