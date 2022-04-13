using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

internal class RoundHelpers
{
	private static List<(int Divider, Money MaxValue)>? RoundCounterDividerAndMaxAmountsCache;

	private static List<(int Divider, Money MaxValue)> RoundCounterDividerAndMaxAmounts
	{
		get
		{
			if (RoundCounterDividerAndMaxAmountsCache == null)
			{
				RoundCounterDividerAndMaxAmountsCache = GenerateRoundCounterDividerAndMaxAmounts();
			}
			return RoundCounterDividerAndMaxAmountsCache;
		}
	}

	private static List<(int Divider, Money MaxValue)> GenerateRoundCounterDividerAndMaxAmounts()
	{
		Money smallestMaximum = Money.Coins(0.1m);
		Money absoluteMaximumInput = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice);

		int level = 0;
		List<(int Divider, Money MaxValue)> roundCounterDividerAndMaxAmounts = new();
		bool end = false;
		do
		{
			var roundDivider = (int)Math.Pow(2, level);
			var maxValue = smallestMaximum * (long)Math.Pow(10, level);
			if (maxValue >= absoluteMaximumInput)
			{
				maxValue = absoluteMaximumInput;
				end = true;
			}
			roundCounterDividerAndMaxAmounts.Insert(0, (roundDivider, maxValue));
			level++;
		}
		while (!end);

		return roundCounterDividerAndMaxAmounts;
	}

	public static Money GetMaxSuggestedAmount(int roundCounter)
	{
		if (roundCounter != 0)
		{
			foreach (var (divider, maxValue) in RoundCounterDividerAndMaxAmounts)
			{
				if (roundCounter % divider == 0)
				{
					return maxValue;
				}
			}
		}

		return RoundCounterDividerAndMaxAmounts.Last().MaxValue;
	}
}
