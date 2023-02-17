using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Affiliation.Models.CoinJoinNotification;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Affiliation;

public class BuiltTransactionData
{
	public BuiltTransactionData(
		IEnumerable<AffiliateInput> inputs,
		IEnumerable<TxOut> outputs,
		Network network,
		CoordinationFeeRate coordinationFeeRate,
		Money minRegistrableAmount)
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

	public Body GetAffiliationData(string affiliationId)
	{
		IEnumerable<Input> inputs = Inputs.Select(x => Input.FromAffiliateInput(x, affiliationId));
		IEnumerable<Output> outputs = Outputs.Select(x => Output.FromTxOut(x));

		return new Body(inputs, outputs, Network.ToSlip44CoinType(), CoordinationFeeRate.Rate, CoordinationFeeRate.PlebsDontPayThreshold.Satoshi, MinRegistrableAmount.Satoshi, GetUnixTimestamp());
	}

	private static long GetUnixTimestamp()
	{
		return ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
	}
}
