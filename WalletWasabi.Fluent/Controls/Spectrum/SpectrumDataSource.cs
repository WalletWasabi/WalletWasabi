using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public abstract class SpectrumDataSource
{
	private float[] _averaged;
	private DispatcherTimer _timer;

	public bool ShouldRender { get; set; }

	public SpectrumDataSource(int numBins, int numAverages, TimeSpan mixInterval)
	{
		Bins = new float[numBins];
		_averaged = new float[numBins];
		NumAverages = numAverages;
	}

	public int NumAverages { get; }

	protected float[] Bins { get; }

	protected int NumBins => Bins.Length;

	protected int MidPointBins => NumBins / 2;

	protected abstract void OnMixData();

	public void Render(ref float[] data)
	{
		OnMixData();

		for (int i = 0; i < NumBins; i++)
		{
			_averaged[i] -= _averaged[i] / NumAverages;
			_averaged[i] += Bins[i] / NumAverages;

			data[i] = Math.Max(data[i], _averaged[i]);
		}
	}

	public void Start()
	{
		ShouldRender = true;
	}


	public void Stop()
	{
		ShouldRender = false;
	}
}
