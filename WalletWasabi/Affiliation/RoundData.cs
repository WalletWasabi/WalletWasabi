using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;
using System.Collections.Generic;

namespace WalletWasabi.Affiliation;

public class RoundData
{
	public RoundData(RoundParameters roundParameters)
	{
		Inputs = new();
		RoundParameters = roundParameters;
	}

	private ConcurrentBag<AffiliateCoin> Inputs { get; }
	private RoundParameters RoundParameters { get; }

	public void AddInput(Coin coin, AffiliationFlag affiliationFlag, bool zeroCoordinationFee)
	{
		Inputs.Add(new AffiliateCoin(coin, affiliationFlag, zeroCoordinationFee || RoundParameters.CoordinationFeeRate.GetFee(coin.Amount) == Money.Zero));
	}

	public FinalizedRoundData Finalize(NBitcoin.Transaction transaction)
	{
		if (!transaction.Inputs.Select(x => x.PrevOut).ToHashSet().SetEquals(Inputs.Select(x => x.Outpoint).ToHashSet()))
		{
			throw new Exception("Inconsistent data.");
		}

		IEnumerable<AffiliateCoin> sortedInputs = transaction.Inputs.Select(x => Inputs.ToList().Find(y => x.PrevOut == y.Outpoint));

		return new(RoundParameters, sortedInputs.ToImmutableList(), transaction);
	}
}
