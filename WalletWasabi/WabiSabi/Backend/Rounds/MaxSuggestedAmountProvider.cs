using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class MaxSuggestedAmountProvider
{
	public MaxSuggestedAmountProvider(WabiSabiConfig config)
	{
		Config = config;
		MaxSuggestedAmount = GetMaxSuggestedAmount();
	}

	private List<DividerMaxValue> RoundCounterDividerAndMaxAmounts { get; set; } = new List<DividerMaxValue>();
	private Money LastGeneratedMaxSuggestedAmountBase { get; set; } = Money.Zero;
	private WabiSabiConfig Config { get; init; }
	private int Counter { get; set; }
	public Money MaxSuggestedAmount { get; private set; }
	private Money AbsoluteMaximumInput { get; } = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice);

	private void CheckOrGenerateRoundCounterDividerAndMaxAmounts()
	{
		Money maxSuggestedAmountBase = Config.MaxSuggestedAmountBase;

		if (maxSuggestedAmountBase == LastGeneratedMaxSuggestedAmountBase)
		{
			// It was already generated for this base.
			return;
		}

		int level = 0;
		List<DividerMaxValue> roundCounterDividerAndMaxAmounts = new();
		bool end = false;
		do
		{
			var roundDivider = (int)Math.Pow(2, level);
			var maxValue = maxSuggestedAmountBase * (long)Math.Pow(10, level);
			if (maxValue >= AbsoluteMaximumInput)
			{
				maxValue = AbsoluteMaximumInput;
				end = true;
			}
			roundCounterDividerAndMaxAmounts.Insert(0, new(roundDivider, maxValue));
			level++;
		}
		while (!end);

		RoundCounterDividerAndMaxAmounts = roundCounterDividerAndMaxAmounts;
		LastGeneratedMaxSuggestedAmountBase = maxSuggestedAmountBase;
		Counter = 0;
	}

	private Money GetMaxSuggestedAmount()
	{
		CheckOrGenerateRoundCounterDividerAndMaxAmounts();
		if (Counter != 0)
		{
			foreach (var (divider, maxValue) in RoundCounterDividerAndMaxAmounts.Where(v => v.MaxValue <= Config.MaxRegistrableAmount))
			{
				if (Counter % divider == 0)
				{
					return maxValue;
				}
			}
		}

		// We always start with the largest whale round.
		return RoundCounterDividerAndMaxAmounts.First().MaxValue;
	}

	private record DividerMaxValue(int Divider, Money MaxValue);

	public void StepMaxSuggested(Round round, bool isInputRegistrationSuccessful)
	{
		if (round is BlameRound)
		{
			return;
		}

		if (!isInputRegistrationSuccessful)
		{
			// We will keep this on the maximum - let everyone join.
			MaxSuggestedAmount = AbsoluteMaximumInput;
			return;
		}

		// Alter the value.
		Counter++;

		MaxSuggestedAmount = GetMaxSuggestedAmount();
	}
}
