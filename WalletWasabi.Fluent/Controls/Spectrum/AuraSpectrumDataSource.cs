using System.Linq;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class AuraSpectrumDataSource : SpectrumDataSource
{
	private readonly Random _random;

	public AuraSpectrumDataSource(int numBins) : base(numBins, 30, TimeSpan.FromSeconds(0.2))
	{
		_random = new Random(DateTime.Now.Millisecond);
	}

	public bool IsActive { get; set; }

	protected override void OnMixData()
	{
		for (int i = 0; i < NumBins; i++)
		{
			Bins[i] = IsActive ? _random.NextSingle() : Bins[i] - 0.1F;
		}

		if (!IsActive && Bins.All(f => f <= 0))
		{
			Stop();
		}
	}
}
