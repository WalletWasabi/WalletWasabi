using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class MaxSuggestedAmountProvider
{
	public MaxSuggestedAmountProvider(WabiSabiConfig config)
	{
		Config = config;
	}

	private List<DividerMaxValue> RoundCounterDividerAndMaxAmounts { get; set; } = new List<DividerMaxValue>();
	private Money LastGeneratedMaxSuggestedAmountBase { get; set; } = Money.Zero;
	private WabiSabiConfig Config { get; init; }

	private void CheckOrGenerateRoundCounterDividerAndMaxAmounts()
	{
		Money maxSuggestedAmountBase = Config.MaxSuggestedAmountBase;

		if (maxSuggestedAmountBase == LastGeneratedMaxSuggestedAmountBase)
		{
			// It was already generated for this base.
			return;
		}

		Money absoluteMaximumInput = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice);

		int level = 0;
		List<DividerMaxValue> roundCounterDividerAndMaxAmounts = new();
		bool end = false;
		do
		{
			var roundDivider = (int)Math.Pow(2, level);
			var maxValue = maxSuggestedAmountBase * (long)Math.Pow(10, level);
			if (maxValue >= absoluteMaximumInput)
			{
				maxValue = absoluteMaximumInput;
				end = true;
			}
			roundCounterDividerAndMaxAmounts.Insert(0, new(roundDivider, maxValue));
			level++;
		}
		while (!end);

		RoundCounterDividerAndMaxAmounts = roundCounterDividerAndMaxAmounts;
		LastGeneratedMaxSuggestedAmountBase = maxSuggestedAmountBase;
	}

	public Money GetMaxSuggestedAmount(int roundCounter)
	{
		CheckOrGenerateRoundCounterDividerAndMaxAmounts();
		if (roundCounter != 0)
		{
			foreach (var (divider, maxValue) in RoundCounterDividerAndMaxAmounts.Where(v => v.MaxValue <= Config.MaxRegistrableAmount))
			{
				if (roundCounter % divider == 0)
				{
					return maxValue;
				}
			}
		}

		return RoundCounterDividerAndMaxAmounts.Last().MaxValue;
	}

	private record DividerMaxValue(int Divider, Money MaxValue);
}
