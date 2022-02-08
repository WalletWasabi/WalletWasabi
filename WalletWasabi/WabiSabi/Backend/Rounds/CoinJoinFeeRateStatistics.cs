using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class CoinJoinFeeRateStatistics
{
	private const int MaximumDaysToStore = 30;
	private List<DateTimeAndFeeRate> FeeRates { get; }

	private CoinJoinFeeRateStatistics(IEnumerable<DateTimeAndFeeRate> dateAndfeeRates)
	{
		FeeRates = new(dateAndfeeRates.OrderBy(x => x.DateTimeOffset));
	}

	public static CoinJoinFeeRateStatistics LoadFromCoinJoinTransactionArchiver(CoinJoinTransactionArchiver coinJoinTransactionArchiver)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var from = now - TimeSpan.FromDays(MaximumDaysToStore);
		var dateAndFeeRates = coinJoinTransactionArchiver.ReadJson(from, now)
			.Select(txinfo => new DateTimeAndFeeRate(
				DateTimeOffset.FromUnixTimeMilliseconds(txinfo.Created),
				new FeeRate(txinfo.FeeRate)));

		return new(dateAndFeeRates);
	}

	public void Add(FeeRate feeRate)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;

		FeeRates.Add(new(now, feeRate));

		// Prune old items.
		DateTimeOffset removeBefore = now - TimeSpan.FromDays(MaximumDaysToStore);
		while (FeeRates.Any() && (FeeRates[0].DateTimeOffset < removeBefore))
		{
			FeeRates.RemoveAt(0);
		}
	}

	public FeeRate GetAvarage(DateTimeOffset from)
	{
		return new FeeRate(FeeRates.Where(x => x.DateTimeOffset >= from).Average(x => x.FeeRate.SatoshiPerByte));
	}

	private record DateTimeAndFeeRate(
		DateTimeOffset DateTimeOffset,
		FeeRate FeeRate);
}
