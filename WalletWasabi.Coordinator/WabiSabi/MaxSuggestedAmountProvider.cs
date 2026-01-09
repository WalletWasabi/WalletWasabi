using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Coordinator.WabiSabi;

public class MaxSuggestedAmountProvider
{
	private readonly List<AmountTier> _amountTiers;
	private int _counter;

	public MaxSuggestedAmountProvider(Money initialMaxSuggestedAmount, Money maxRegistrableAmount)
	{
		_amountTiers = BuildAmountTiers(initialMaxSuggestedAmount, maxRegistrableAmount);
		MaxSuggestedAmount = _amountTiers[0].MaxValue;
	}

	public Money MaxSuggestedAmount { get; private set; }

	private static List<AmountTier> BuildAmountTiers(Money initialMaxSuggestedAmount, Money maxRegistrableAmount) =>
		Enumerable
			.Range(0, 30)
			.Select(level => new
			{
				divider = 1 << level,
				maxValue = initialMaxSuggestedAmount * (long)Math.Pow(10, level)
			})
			.TakeWhile(x => x.maxValue < maxRegistrableAmount)
			.Select(x => new AmountTier(x.divider, x.maxValue))
			.Concat([new AmountTier(1 << 31, maxRegistrableAmount)])
			.OrderByDescending(t => t.MaxValue)
			.ToList();


	public void ResetMaxSuggested()
	{
		// We will keep this on the maximum - let everyone join.
		MaxSuggestedAmount = _amountTiers[0].MaxValue;
		_counter = -1;
	}

	public void StepMaxSuggested()
	{
		// Alter the value.
		_counter++;

		var tier = _amountTiers.FirstOrDefault(t => _counter % t.Divider == 0);
		MaxSuggestedAmount = tier?.MaxValue ?? _amountTiers[0].MaxValue;
	}

	private record AmountTier(int Divider, Money MaxValue);
}
