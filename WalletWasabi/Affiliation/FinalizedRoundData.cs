using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;
using WalletWasabi.Affiliation.Extensions;

namespace WalletWasabi.Affiliation;

public class FinalizedRoundData
{
	public FinalizedRoundData(RoundParameters roundParameters, ImmutableList<AffiliateCoin> inputs, Transaction transaction)
	{
		RoundParameters = roundParameters;
		Inputs = inputs;
		Transaction = transaction;
	}

	private RoundParameters RoundParameters { get; }
	private ImmutableList<AffiliateCoin> Inputs { get; }
	private NBitcoin.Transaction Transaction { get; set; }

	public Body GetAffiliationData(AffiliationFlag affiliationFlag)
	{
		return GetAffiliationData(RoundParameters, Inputs, Transaction, affiliationFlag);
	}

	private static Body GetAffiliationData(RoundParameters roundParameters, IEnumerable<AffiliateCoin> Inputs, NBitcoin.Transaction transaction, AffiliationFlag affiliationFlag)
	{
		IEnumerable<Input> inputs = Inputs.Select(x => Input.FromCoin(x, x.ZeroCoordinationFee, x.AffiliationFlag == affiliationFlag));
		IEnumerable<Output> outputs = transaction.Outputs.Select<TxOut, Output>(x => Output.FromTxOut(x));

		return new Body(inputs, outputs, roundParameters.Network.ToSlip44CoinType(), roundParameters.CoordinationFeeRate.Rate, roundParameters.CoordinationFeeRate.PlebsDontPayThreshold.Satoshi, roundParameters.AllowedInputAmounts.Min.Satoshi, GetUnixTimestamp());
	}

	private static long GetUnixTimestamp()
	{
		return ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
	}
}
