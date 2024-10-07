using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

/// <summary>
/// Generator of randomness for <see cref="CoinJoinCoinSelector"/>.
/// </summary>
public class CoinJoinCoinSelectorRandomnessGenerator
{
	public CoinJoinCoinSelectorRandomnessGenerator(int maxInputsCount, WasabiRandom rnd)
	{
		Rnd = rnd;
		_maxInputsCount = maxInputsCount;

		// Until our UTXO count target isn't reached, let's register as few coins as we can to reach it.
		for (int i = 1; i <= maxInputsCount; i++)
		{
			Distance.Add(i, Math.Abs(i - maxInputsCount));
		}
	}

	public WasabiRandom Rnd { get; }
	private readonly int _maxInputsCount;
	private Dictionary<int, int> Distance { get; } = new();

	public virtual int GetRandomBiasedSameTxAllowance(int percent)
	{
		for (int num = 0; num <= 100; num++)
		{
			if (Rnd.GetInt(1, 101) <= percent)
			{
				return num;
			}
		}

		return 0;
	}

	/// <summary>
	/// Calculates how many inputs are desirable to be registered.
	/// Note: Random biasing is applied.
	/// </summary>
	/// <returns>A number: 1, 2, .., <see cref="CoinJoinCoinSelector.MaxInputsRegistrableByWallet"/>.</returns>
	public virtual int GetInputTarget()
	{
		foreach (var best in Distance.OrderBy(x => x.Value))
		{
			if (Rnd.GetInt(0, 10) < 5)
			{
				return best.Key;
			}
		}

		return _maxInputsCount;
	}
}
