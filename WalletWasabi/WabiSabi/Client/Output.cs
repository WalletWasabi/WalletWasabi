using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client;

public record Output
{
	private Output(Money amount, ScriptType scriptType, FeeRate feeRate, bool isEffectiveCost)
	{
		ScriptType = scriptType;
		Fee = feeRate.GetFee(scriptType.EstimateOutputVsize());
		InputFee = feeRate.GetFee(scriptType.EstimateInputVsize());

		// The value of amount is defined as the effective cost or a denomination amount.
		Amount = isEffectiveCost ? amount - Fee : amount;
	}

	public Money Amount { get; }
	public ScriptType ScriptType { get; }
	public Money EffectiveAmount => Amount - Fee;
	public Money EffectiveCost => Amount + Fee;
	public Money InputFee { get; }
	public Money Fee { get; }

	public static Output FromDenomination(Money amount, ScriptType scriptType, FeeRate feeRate)
	{
		return new Output(amount, scriptType, feeRate, false);
	}

	public static Output FromAmount(Money amount, ScriptType scriptType, FeeRate feeRate)
	{
		return new Output(amount, scriptType, feeRate, true);
	}
}
