namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SplashEffectDataSource : SpectrumDataSource
{
	private int _currentEffectIndex;

	public SplashEffectDataSource(int numBins) : base(numBins, 4, TimeSpan.FromSeconds(0.005))
	{
	}

	protected override void OnMixData()
	{
		Bins[MidPointBins - _currentEffectIndex] = 1;
		Bins[MidPointBins + _currentEffectIndex] = 1;

		for (int i = _currentEffectIndex; i > 0; i--)
		{
			Bins[MidPointBins - i] -= 0.1f;
			Bins[MidPointBins + i] -= 0.1f;
		}

		_currentEffectIndex++;

		if (_currentEffectIndex >= MidPointBins)
		{
			_currentEffectIndex = 0;

			for (var i = 0; i < NumBins; i++)
			{
				Bins[i] = 0;
			}

			Stop();
		}
	}
}
