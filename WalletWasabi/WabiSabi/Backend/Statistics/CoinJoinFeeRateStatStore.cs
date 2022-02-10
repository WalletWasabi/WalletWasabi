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
	private const int MaximumDaysToStore = 30;
	private List<CoinJoinFeeRateStatRecord> CoinJoinFeeRateStatRecords { get; }

	private CoinJoinFeeRateAvarage[] DefaultAvarages { get; set; } = Array.Empty<CoinJoinFeeRateAvarage>();

	public CoinJoinFeeRateStatStore(WabiSabiConfig config, IRPCClient rpc, IEnumerable<CoinJoinFeeRateStatRecord> feeRateStatRecords) :
		base(TimeSpan.FromMinutes(10))
	{
		Config = config;
		Rpc = rpc;
		CoinJoinFeeRateStatRecords = new(feeRateStatRecords.OrderBy(x => x.DateTimeOffset));
	}

	private WabiSabiConfig Config { get; }
	private IRPCClient Rpc { get; }

	public event EventHandler<CoinJoinFeeRateStatRecord>? NewStat;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancel).ConfigureAwait(false)).FeeRate;

		CoinJoinFeeRateStatRecord record = new(DateTimeOffset.UtcNow, Config.ConfirmationTarget, feeRate);
		Add(record);
		NewStat?.Invoke(this, record);
	}

	public void Add(CoinJoinFeeRateStatRecord feeRateStatRecord)
	{
		CoinJoinFeeRateStatRecords.Add(feeRateStatRecord);

		var timeFrames = new[]
{
			TimeSpan.FromDays(1),
			TimeSpan.FromDays(7),
			TimeSpan.FromDays(30),
		};

		DefaultAvarages = timeFrames.Select(t => new CoinJoinFeeRateAvarage((int)Math.Floor(t.TotalHours), GetAvarage(t))).ToArray();

		// Prune old items.
		DateTimeOffset removeBefore = DateTimeOffset.UtcNow - TimeSpan.FromDays(MaximumDaysToStore);
		while (CoinJoinFeeRateStatRecords.Any() && CoinJoinFeeRateStatRecords[0].DateTimeOffset < removeBefore)
		{
			CoinJoinFeeRateStatRecords.RemoveAt(0);
		}
	}

	public FeeRate GetAvarage(TimeSpan timeFrame)
	{
		var from = DateTimeOffset.UtcNow - timeFrame;
		return new FeeRate(CoinJoinFeeRateStatRecords.Where(x => x.DateTimeOffset >= from).Average(x => x.FeeRate.SatoshiPerByte));
	}

	public CoinJoinFeeRateAvarage[] GetDefaultAvarages()
	{
		return DefaultAvarages;
	}

	public static CoinJoinFeeRateStatStore LoadFromFile(string filePath, WabiSabiConfig config, IRPCClient rpc)
	{
		var now = DateTimeOffset.UtcNow;
		var from = now - TimeSpan.FromDays(MaximumDaysToStore);

		var records = !File.Exists(filePath) ?
			Enumerable.Empty<CoinJoinFeeRateStatRecord>() :
			File.ReadAllLines(filePath)
				.Select(x => CoinJoinFeeRateStatRecord.FromLine(x))
				.Where(x => x.DateTimeOffset > from);

		var store = new CoinJoinFeeRateStatStore(config, rpc, records);

		return store;
	}
}
