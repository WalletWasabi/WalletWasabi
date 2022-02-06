using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls;

public class SpectrumControl : TemplatedControl
{
	private readonly Random _random;
	private Pen _linePen;
	private ISolidColorBrush _lineBrush;
	private IConicGradientBrush _gradientBrush;
	private DispatcherTimer _timer;
	private double[] _bins;
	private double[] _averaged;
	private const int NumBins = 250;
	private const int NumAverages = 30;

	public static readonly StyledProperty<bool> IsActiveProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsActive));

	public SpectrumControl()
	{
		_bins = new double[NumBins];
		_averaged = new double[NumBins];

		_random = new Random(DateTime.Now.Millisecond);
		_lineBrush = SolidColorBrush.Parse("#97D234");
		Background = new RadialGradientBrush()
		{
			GradientStops =
			{
				new GradientStop { Color = Color.Parse("#00000D21"), Offset = 0 },
				new GradientStop { Color = Color.Parse("#FF000D21"), Offset = 1 }
			}
		};

		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(0.2)
		};

		_timer.Tick += TimerOnTick;
	}

	private void TimerOnTick(object? sender, EventArgs e)
	{
		var isActive = IsActive;

		for (int i = 0; i < NumBins; i++)
		{
			_bins[i] = isActive ? _random.NextDouble() : 0;
		}

		if (!isActive)
		{
			_timer.Stop();
		}
	}

	private void OnIsActiveChanged()
	{
		if (IsActive)
		{
			_timer.Start();
		}
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsActiveProperty)
		{
			OnIsActiveChanged();
		}
	}

	public bool IsActive
	{
		get => GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		_linePen = new Pen(_lineBrush, finalSize.Width / NumBins);

		return base.ArrangeOverride(finalSize);
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		var thickness = Bounds.Width / NumBins;
		var center = (Bounds.Width / 2);

		double x = 0;

		for (int i = 0; i < NumBins; i++)
		{
			_averaged[i] -= _averaged[i] / NumAverages;
			_averaged[i] += _bins[i] / NumAverages;

			var dCenter = Math.Abs(x - center);
			var multiplier = 1 - (dCenter / center);

			context.DrawLine(_linePen, new Point(x, Bounds.Height),
				new Point(x, Bounds.Height - multiplier * _averaged[i] *  (Bounds.Height * 0.95)));
			x += thickness;
		}

		context.FillRectangle(Background, Bounds);

		Dispatcher.UIThread.Post(() => InvalidateVisual());
	}
}