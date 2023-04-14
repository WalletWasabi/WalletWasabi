namespace WalletWasabi.Fluent.Controls.Spectrum;

public class AuraSpectrumDataSource : SpectrumDataSource
{
	private readonly Random _random;

	public AuraSpectrumDataSource(int numBins) : base(numBins, 30, TimeSpan.FromSeconds(0.2))
	{
		_random = new Random(DateTime.Now.Millisecond);
	}

	protected override void OnMixData()
	{
		for (int i = 0; i < NumBins; i++)
		{
			Bins[i] = _random.NextSingle();
		}
	}
}
