using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class FeeRateStatStore : PeriodicRunner
{
	private const int MaximumDaysToStore = 30;
	private List<FeeRateStatRecord> FeeRateStatRecords { get; }

	public FeeRateStatStore(WabiSabiConfig config, IRPCClient rpc, IEnumerable<FeeRateStatRecord> feeRateStatRecords) :
		base(TimeSpan.FromMinutes(10))
	{
		Config = config;
		Rpc = rpc;
		FeeRateStatRecords = new(feeRateStatRecords.OrderBy(x => x.DateTimeOffset));
	}

	public WabiSabiConfig Config { get; }
	public IRPCClient Rpc { get; }

	public event EventHandler<FeeRateStatRecord>? NewStat;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancel).ConfigureAwait(false)).FeeRate;

		FeeRateStatRecord record = new(DateTimeOffset.UtcNow, Config.ConfirmationTarget, feeRate);
		Add(record);
		NewStat?.Invoke(this, record);
	}

	public void Add(FeeRateStatRecord feeRateStatRecord)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;

		FeeRateStatRecords.Add(feeRateStatRecord);

		// Prune old items.
		DateTimeOffset removeBefore = now - TimeSpan.FromDays(MaximumDaysToStore);
		while (FeeRateStatRecords.Any() && FeeRateStatRecords[0].DateTimeOffset < removeBefore)
		{
			FeeRateStatRecords.RemoveAt(0);
		}
	}

	public FeeRate GetAvarage(TimeSpan timeFrame)
	{
		var from = DateTimeOffset.UtcNow - timeFrame;
		return new FeeRate(FeeRateStatRecords.Where(x => x.DateTimeOffset >= from).Average(x => x.FeeRate.SatoshiPerByte));
	}

	public static FeeRateStatStore LoadFromFile(string filePath, WabiSabiConfig config, IRPCClient rpc)
	{
		var now = DateTimeOffset.UtcNow;
		var from = now - TimeSpan.FromDays(MaximumDaysToStore);

		var records = !File.Exists(filePath) ?
			Enumerable.Empty<FeeRateStatRecord>() :
			File.ReadAllLines(filePath)
				.Select(x => FeeRateStatRecord.FromLine(x))
				.Where(x => x.DateTimeOffset > from);

		var store = new FeeRateStatStore(config, rpc, records);

		return store;
	}
}
