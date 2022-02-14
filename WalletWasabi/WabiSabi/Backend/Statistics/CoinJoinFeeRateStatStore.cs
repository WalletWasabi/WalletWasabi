using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class CoinJoinFeeRateStatStore : PeriodicRunner
{
	public CoinJoinFeeRateStatStore(WabiSabiConfig config, IRPCClient rpc, IEnumerable<CoinJoinFeeRateStat> feeRateStats)
		: base(TimeSpan.FromMinutes(10))
	{
		Config = config;
		Rpc = rpc;
		CoinJoinFeeRateStats = new(feeRateStats.OrderBy(x => x.DateTimeOffset));
	}

	public CoinJoinFeeRateStatStore(WabiSabiConfig config, IRPCClient rpc)
		: this(config, rpc, Enumerable.Empty<CoinJoinFeeRateStat>())
	{
	}

	private static TimeSpan[] TimeFrames { get; } = new[]
	{
		TimeSpan.FromDays(1),
		TimeSpan.FromDays(7),
		TimeSpan.FromDays(30),
	};

	private static TimeSpan MaximumTimeToStore { get; } = TimeFrames.Max();

	private List<CoinJoinFeeRateStat> CoinJoinFeeRateStats { get; }

	private CoinJoinFeeRateAverage[] DefaultAverages { get; set; } = Array.Empty<CoinJoinFeeRateAverage>();

	private WabiSabiConfig Config { get; }
	private IRPCClient Rpc { get; }

	public event EventHandler<CoinJoinFeeRateStat>? NewStat;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancel).ConfigureAwait(false)).FeeRate;

		CoinJoinFeeRateStat feeRateStat = new(DateTimeOffset.UtcNow, Config.ConfirmationTarget, feeRate);
		Add(feeRateStat);
		NewStat?.Invoke(this, feeRateStat);
	}

	private void Add(CoinJoinFeeRateStat feeRateStat)
	{
		CoinJoinFeeRateStats.Add(feeRateStat);

		DefaultAverages = TimeFrames.Select(t => new CoinJoinFeeRateAverage(t, GetAverage(t))).ToArray();

		// Prune old items.
		DateTimeOffset removeBefore = DateTimeOffset.UtcNow - MaximumTimeToStore;
		while (CoinJoinFeeRateStats.Any() && CoinJoinFeeRateStats[0].DateTimeOffset < removeBefore)
		{
			CoinJoinFeeRateStats.RemoveAt(0);
		}
	}

	private FeeRate GetAverage(TimeSpan timeFrame)
	{
		var from = DateTimeOffset.UtcNow - timeFrame;
		return new FeeRate(CoinJoinFeeRateStats.Where(x => x.DateTimeOffset >= from).Average(x => x.FeeRate.SatoshiPerByte));
	}

	/// <summary>
	/// The avagares are calculated periodically in every <see cref="PeriodicRunner.Period"/> time span.
	/// </summary>
	public CoinJoinFeeRateAverage[] GetDefaultAverages()
	{
		return DefaultAverages;
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
