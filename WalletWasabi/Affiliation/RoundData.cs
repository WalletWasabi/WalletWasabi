using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Affiliation;

public class RoundData
{
	public RoundData(RoundParameters roundParameters)
	{
		RoundParameters = roundParameters;
	}

	private RoundParameters RoundParameters { get; }
	private Dictionary<OutPoint, AffiliateInput> AffiliateInputsByOutpoint { get; } = new();

	public void AddInputCoin(Coin coin, bool isCoordinationFeeExempted)
	{
		AffiliateInputsByOutpoint[coin.Outpoint] = 
			new AffiliateInput(
				coin.Outpoint,
				coin.ScriptPubKey,
				AffiliationConstants.DefaultAffiliationId,
				isCoordinationFeeExempted || IsNoFee(coin.Amount));
	}

	public void AddInputAffiliationId(Coin coin, string affiliationId)
	{
		var inputData = AffiliateInputsByOutpoint[coin.Outpoint];
		AffiliateInputsByOutpoint[coin.Outpoint] = inputData with { AffiliationId = affiliationId };
	}

	public BuiltTransactionData FinalizeRoundData(Transaction transaction)
	{
		IEnumerable<AffiliateInput> inputs = transaction.Inputs
			.Select(x => AffiliateInputsByOutpoint[x.PrevOut])
			.ToArray();

		return new BuiltTransactionData(inputs, transaction.Outputs, RoundParameters.Network, RoundParameters.CoordinationFeeRate, RoundParameters.AllowedInputAmounts.Min);
	}

	private bool IsNoFee(Money amount) => RoundParameters.CoordinationFeeRate.GetFee(amount) == Money.Zero;
}
