using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class CoinJoinFeeRateStatStore : PeriodicRunner
{
	public CoinJoinFeeRateStatStore(WabiSabiConfig config, IRPCClient rpc, IEnumerable<CoinJoinFeeRateStat> feeRateStats)
		: base(TimeSpan.FromMinutes(10))
	{
		_config = config;
		_rpc = rpc;
		_coinJoinFeeRateStats = new(feeRateStats.OrderBy(x => x.DateTimeOffset));
	}

	public CoinJoinFeeRateStatStore(WabiSabiConfig config, IRPCClient rpc)
		: this(config, rpc, Enumerable.Empty<CoinJoinFeeRateStat>())
	{
	}

	public event EventHandler<CoinJoinFeeRateStat>? NewStat;

	private static TimeSpan[] TimeFrames { get; } = Constants.CoinJoinFeeRateMedianTimeFrames.Select(tf => TimeSpan.FromHours(tf)).ToArray();

	private static TimeSpan MaximumTimeToStore { get; } = TimeFrames.Max();

	private readonly List<CoinJoinFeeRateStat> _coinJoinFeeRateStats;

	private CoinJoinFeeRateMedian[] DefaultMedians { get; set; } = Array.Empty<CoinJoinFeeRateMedian>();

	private readonly WabiSabiConfig _config;
	private readonly IRPCClient _rpc;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var feeRate = (await _rpc.EstimateConservativeSmartFeeAsync((int)_config.ConfirmationTarget, cancel).ConfigureAwait(false)).FeeRate;

		CoinJoinFeeRateStat feeRateStat = new(DateTimeOffset.UtcNow, _config.ConfirmationTarget, feeRate);
		Add(feeRateStat);
		NewStat?.Invoke(this, feeRateStat);
	}

	private void Add(CoinJoinFeeRateStat feeRateStat)
	{
		_coinJoinFeeRateStats.Add(feeRateStat);

		DefaultMedians = TimeFrames.Select(t => new CoinJoinFeeRateMedian(t, GetMedian(t))).ToArray();

		// Prune old items.
		DateTimeOffset removeBefore = DateTimeOffset.UtcNow - MaximumTimeToStore;
		while (_coinJoinFeeRateStats.Count != 0 && _coinJoinFeeRateStats[0].DateTimeOffset < removeBefore)
		{
			_coinJoinFeeRateStats.RemoveAt(0);
		}
	}

	private FeeRate GetMedian(TimeSpan timeFrame)
	{
		var from = DateTimeOffset.UtcNow - timeFrame;
		var feeRates = _coinJoinFeeRateStats
			.Where(x => x.DateTimeOffset >= from)
			.OrderByDescending(x => x.FeeRate.SatoshiPerByte)
			.ToArray();

		// If the median is even, then it's the average of the middle two numbers.
		FeeRate med = feeRates.Length % 2 == 0
			? new FeeRate((feeRates[feeRates.Length / 2].FeeRate.SatoshiPerByte + feeRates[(feeRates.Length / 2) - 1].FeeRate.SatoshiPerByte) / 2)
			: feeRates[feeRates.Length / 2].FeeRate;

		return med;
	}

	/// <summary>
	/// The medians are calculated periodically in every <see cref="PeriodicRunner.Period"/> time span.
	/// </summary>
	public CoinJoinFeeRateMedian[] GetDefaultMedians()
	{
		return DefaultMedians;
	}

	public static CoinJoinFeeRateStatStore LoadFromFile(string filePath, WabiSabiConfig config, IRPCClient rpc)
	{
		var from = DateTimeOffset.UtcNow - MaximumTimeToStore;

		var stats = !File.Exists(filePath)
			? Enumerable.Empty<CoinJoinFeeRateStat>()
			: File.ReadAllLines(filePath)
				.Select(x => CoinJoinFeeRateStat.FromLine(x))
				.Where(x => x.DateTimeOffset >= from);

		var store = new CoinJoinFeeRateStatStore(config, rpc, stats);

		return store;
	}
}
