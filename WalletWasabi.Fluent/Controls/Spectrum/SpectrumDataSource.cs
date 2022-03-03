using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public abstract class SpectrumDataSource
{
	private float[] _bins;
	private float[] _averaged;
	private DispatcherTimer _timer;

	public SpectrumDataSource(int numBins, int numAverages, TimeSpan mixInterval)
	{
		_bins = new float[numBins];
		_averaged = new float[numBins];

		_timer = new DispatcherTimer
		{
			Interval = mixInterval
		};

		_timer.Tick += TimerOnTick;

		NumAverages = numAverages;
	}

	private void TimerOnTick(object? sender, EventArgs e)
	{
		OnMixData();
	}

	public int NumAverages { get; }

	protected float[] Bins => _bins;

	protected int NumBins => Bins.Length;

	protected int MidPointBins => NumBins /2;

	protected abstract void OnMixData();

	public void Render(ref float[] data)
	{
		for (int i = 0; i < NumBins; i++)
		{
			_averaged[i] -= _averaged[i] / NumAverages;
			_averaged[i] += Bins[i] / NumAverages;

			data[i] = Math.Max(data[i], _averaged[i]);
		}
	}

	public void Start()
	{
		_timer.Start();
	}

	public void Stop()
	{
		_timer.Stop();
	}
}