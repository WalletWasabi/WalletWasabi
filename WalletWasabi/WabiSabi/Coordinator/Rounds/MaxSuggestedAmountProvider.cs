namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public class MaxSuggestedAmountProvider
{
	public MaxSuggestedAmountProvider(WabiSabiConfig config)
	{
		MaxSuggestedAmount = GetMaxSuggestedAmount(config);
	}

	/// <summary>Maps round frequencies to their corresponding maximum suggested amounts.</summary>
	/// <remarks>Bigger suggested amounts are used for less frequent rounds.</remarks>
	private Stack<RoundFrequencyAndMaxValue> RoundFrequencyAndMaxAmounts { get; } = new();
	private Money LastGeneratedMaxSuggestedAmountBase { get; set; } = Money.Zero;

	/// <summary>Number of consecutive successful rounds where input-reg phase succeeded.</summary>
	private int Counter { get; set; }
	public Money MaxSuggestedAmount { get; private set; }

	private void CheckOrGenerateFrequencyTable(WabiSabiConfig config)
	{
		if (config.MaxSuggestedAmountBase == LastGeneratedMaxSuggestedAmountBase)
		{
			// It was already generated for this base.
			return;
		}

		// Recompute the round frequency and max amounts for the new base.
		Counter = 0;
		LastGeneratedMaxSuggestedAmountBase = config.MaxSuggestedAmountBase;
		RoundFrequencyAndMaxAmounts.Clear();

		int level = 0;
		bool end = false;
		do
		{
			var roundDivider = (int)Math.Pow(2, level);
			var maxValue = config.MaxSuggestedAmountBase * (long)Math.Pow(10, level);
			if (maxValue >= config.MaxRegistrableAmount)
			{
				maxValue = config.MaxRegistrableAmount;
				end = true;
			}
			RoundFrequencyAndMaxAmounts.Push(new(roundDivider, maxValue));
			level++;
		}
		while (!end);
	}

	private Money GetMaxSuggestedAmount(WabiSabiConfig config)
	{
		CheckOrGenerateFrequencyTable(config);

		if (Counter != 0)
		{
			foreach (var (frequency, maxValue) in RoundFrequencyAndMaxAmounts.Where(v => v.MaxValue <= config.MaxRegistrableAmount))
			{
				if (Counter % frequency == 0)
				{
					return maxValue;
				}
			}
		}

		// We always start with the largest whale round.
		return RoundFrequencyAndMaxAmounts.First().MaxValue;
	}

	public void StepMaxSuggested(Round round, bool wasInputRegistrationSuccessful)
	{
		if (round is BlameRound)
		{
			return;
		}

		if (!wasInputRegistrationSuccessful)
		{
			// We will keep this on the maximum - let everyone join.
			MaxSuggestedAmount = round.Config.MaxRegistrableAmount;
			return;
		}

		// On successful input registration, alter the value.
		Counter++;
		MaxSuggestedAmount = GetMaxSuggestedAmount(round.Config);
	}

	/// <summary>
	/// This record stores the frequency of rounds and the corresponding maximum value for that frequency.
	/// </summary>
	/// <param name="Frequency">The frequency denoting every nth round.</param>
	/// <remarks>For example, frequency <c>32</c> means that every 32nd successful round will use the <see cref="MaxValue"/> value.</remarks>
	private record RoundFrequencyAndMaxValue(int Frequency, Money MaxValue);
}
