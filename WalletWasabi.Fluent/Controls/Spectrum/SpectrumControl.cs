using System.Collections.Generic;
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

public class SpectrumControl : TemplatedControl, ICustomDrawOperation
{

	private ImmutablePen _linePen;
	private IBrush _lineBrush;

	private readonly AuraSpectrumDataSource _auraSpectrumDataSource;
	private readonly SplashEffectDataSource _splashEffectDataSource;

	private readonly SpectrumDataSource[] _sources;

	private float[] _data;
	private const int NumBins = 250;

	public static readonly StyledProperty<bool> IsActiveProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsActive));

	public static readonly StyledProperty<bool> IsDockEffectVisibleProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsDockEffectVisible));

	public SpectrumControl()
	{

		_data = new float[NumBins];
		_auraSpectrumDataSource = new AuraSpectrumDataSource(NumBins);
		_splashEffectDataSource = new SplashEffectDataSource(NumBins);

		_sources = new SpectrumDataSource[] { _auraSpectrumDataSource, _splashEffectDataSource };

		_lineBrush = SolidColorBrush.Parse("#97D234").ToImmutable();

		Background = new RadialGradientBrush()
		{
			GradientStops =
			{
				new GradientStop { Color = Color.Parse("#00000D21"), Offset = 0 },
				new GradientStop { Color = Color.Parse("#FF000D21"), Offset = 1 }
			}
		};
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
			if (change.NewValue.GetValueOrDefault<bool>())
			{
				_splashEffectDataSource.Start();
			}
		}
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

	protected override Size ArrangeOverride(Size finalSize)
	{
		_linePen = new Pen(_lineBrush, finalSize.Width / NumBins).ToImmutable();

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

			context.DrawLine(_linePen, new Point(x, Bounds.Height),
				new Point(x, Bounds.Height - multiplier * _data[i] *  (Bounds.Height * 0.8)));

			x += thickness;
		}
	}

	void IDisposable.Dispose()
	{
	}

	bool IDrawOperation.HitTest(Point p) => Bounds.Contains(p);

	void IDrawOperation.Render(IDrawingContextImpl context)
	{
		var bounds = Bounds;

		if (context is not ISkiaDrawingContextImpl skia)
		{
			return;
		}

		using (var barsLayer =
		       DrawingContextHelper.CreateDrawingContext(bounds.Size, new Vector(96, 96), skia.GrContext))
		{
			RenderBars(barsLayer);

			using (var filter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp))
			using (var paint = new SKPaint { ImageFilter = filter })
			{
				barsLayer.DrawTo(skia, paint);
			}
		}
	}

	bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => false;
}