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

	private IBrush? _lineBrush;
	private SKPaint? _linePaint;

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

		DispatcherTimer.Run(() =>
		{
			InvalidateVisual();
			return true;
		}, TimeSpan.FromMilliseconds(50), DispatcherPriority.Render);
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
					Style = SKPaintStyle.Fill
				};
			}

			InvalidateArrange();
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

		// Dispatcher.UIThread.Post(() => InvalidateVisual());
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
			var rect = new SKRect(
				(float) x,
				(float) height,
				(float) (x + thickness),
				(float) (height - multiplier * _data[i] * (height * 0.8)));
			context.DrawRect(rect, _linePaint);

			x += thickness;
		}
	}

	private SKPicture RenderPicture(float width, float height)
	{
		using var pictureRecorder = new SKPictureRecorder();
		pictureRecorder.BeginRecording(SKRect.Create(0f, 0f, width, height));

		using var filter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp);
		using var paint = new SKPaint { ImageFilter = filter };

		pictureRecorder.RecordingCanvas.SaveLayer(paint);

		RenderBars(pictureRecorder.RecordingCanvas);

		pictureRecorder.RecordingCanvas.Restore();

		return pictureRecorder.EndRecording();
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

		var width = (float) bounds.Size.Width;
		var height = (float) bounds.Size.Height;
		using var picture = RenderPicture(width, height);

		skia.SkCanvas.DrawPicture(picture);
	}

	bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => false;
}
