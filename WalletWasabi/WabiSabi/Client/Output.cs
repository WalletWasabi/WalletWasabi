using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client;

public class Output : IEqualityComparer<Output>
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

	public bool Equals(Output? x, Output? y)
	{
		if (x is null || y is null)
		{
			if (x is null && y is null)
			{
				return true;
			}
			return false;
		}

		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x.Amount == y.Amount && x.ScriptType == y.ScriptType && x.Fee == y.Fee)
		{
			return true;
		}

		return false;
	}

	public int GetHashCode([DisallowNull] Output obj) => HashCode.Combine(Amount, ScriptType, Fee);
}
