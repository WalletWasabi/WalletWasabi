using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Affiliation;

public class FinalizedRoundData
{
	public FinalizedRoundData(IEnumerable<AffiliateInput> inputs, IEnumerable<TxOut> outputs, Network network, CoordinationFeeRate coordinationFeeRate, Money minRegistrableAmount)
	{
		Inputs = inputs;
		Outputs = outputs;
		Network = network;
		CoordinationFeeRate = coordinationFeeRate;
		MinRegistrableAmount = minRegistrableAmount;
	}

	public IEnumerable<AffiliateInput> Inputs { get; }
	public IEnumerable<TxOut> Outputs { get; }
	public Network Network { get; }
	public CoordinationFeeRate CoordinationFeeRate { get; }
	public Money MinRegistrableAmount { get; }

	public Body GetAffiliationData(AffiliationFlag affiliationFlag)
	{
		IEnumerable<Input> inputs = Inputs.Select(x => Input.FromAffiliateInput(x, affiliationFlag));
		IEnumerable<Output> outputs = Outputs.Select(x => Output.FromTxOut(x));

		return new Body(inputs, outputs, Network.ToSlip44CoinType(), CoordinationFeeRate.Rate, CoordinationFeeRate.PlebsDontPayThreshold.Satoshi, MinRegistrableAmount.Satoshi, GetUnixTimestamp());
	}

	private static long GetUnixTimestamp()
	{
		return ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
	}
}

public record AffiliateInput
{
	public AffiliateInput(OutPoint prevout, Script scriptPubKey, AffiliationFlag affiliationFlag, bool isNoFee)
	{
		Prevout = prevout;
		ScriptPubKey = scriptPubKey;
		AffiliationFlag = affiliationFlag;
		IsNoFee = isNoFee;
	}

	public AffiliateInput(Coin coin, AffiliationFlag affiliationFlag, bool isNoFee)
		  : this(coin.Outpoint, coin.ScriptPubKey, affiliationFlag, isNoFee)
	{
	}

	public OutPoint Prevout { get; }
	public Script ScriptPubKey { get; }
	public AffiliationFlag AffiliationFlag { get; }
	public bool IsNoFee { get; }
}
