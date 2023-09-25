using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumControlState
{
	public const int NumBins = 64;
	public const double TextureHeight = 32;
	public const double TextureWidth = 32;

	internal SKColor _lineColor;
	internal SKSurface? _surface;

	private readonly SpectrumDataSource[] _sources;

	internal readonly SKPaint _blur = new()
	{
		ImageFilter = SKImageFilter.CreateBlur(24, 24, SKShaderTileMode.Clamp),
		FilterQuality = SKFilterQuality.Low
	};

	internal IBrush? _lineBrush;

	internal float[] _data;

	private bool _isGenerating;

	private readonly AuraSpectrumDataSource _auraSpectrumDataSource;
	internal readonly SplashEffectDataSource _splashEffectDataSource;

	private readonly DispatcherTimer _invalidationTimer;

	public SpectrumControlState(SpectrumControl control)
	{
		Control = control;

		_data = new float[NumBins];
		_auraSpectrumDataSource = new AuraSpectrumDataSource(NumBins);
		_splashEffectDataSource = new SplashEffectDataSource(NumBins);

		_auraSpectrumDataSource.GeneratingDataStateChanged += OnGeneratingDataStateChanged;
		_splashEffectDataSource.GeneratingDataStateChanged += OnGeneratingDataStateChanged;

		_sources = new SpectrumDataSource[] { _auraSpectrumDataSource, _splashEffectDataSource };

		_invalidationTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(1000.0 / 15.0)
		};

		_invalidationTimer.Tick += (sender, args) => Control.InvalidateVisual();
	}

	private SpectrumControl Control { get; }

	private void OnGeneratingDataStateChanged(object? sender, EventArgs e)
	{
		_isGenerating = _auraSpectrumDataSource.IsGenerating || _splashEffectDataSource.IsGenerating;

		if (_isGenerating)
		{
			_invalidationTimer.Start();
		}
	}

	public void OnIsActiveChanged()
	{
		if (Control.IsActive)
		{
			_auraSpectrumDataSource.Start();
		}
		else
		{
			_auraSpectrumDataSource.Stop();
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

		context.Custom(new SpectrumDrawOperation(Control.Bounds, this));
	}
}
