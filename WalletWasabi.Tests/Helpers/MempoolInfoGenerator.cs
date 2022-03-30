using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Tests.Helpers;

public class MempoolInfoGenerator
{
	private static readonly int[] FeeLimits = new[]
	{
			1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 17, 20, 25, 30, 40, 50, 60, 70, 80,
			100, 120, 140, 170, 200, 250, 300, 400, 500, 600, 700, 800, 1000,
			1200, 1400, 1700, 2000, 2500, 3000, 4000, 5000, 6000, 7000, 8000, 10000
		};

	public static (int from, int to)[] FeeRanges { get; } = FeeLimits.Zip(FeeLimits.Skip(1), (from, to) => (from, to)).ToArray();

	public static FeeRate GenerateFeeRateForTarget(int target)
		=> new((decimal)(4_000 / (target * target) * Random.Shared.Gaussian(1.0, 0.2)));

	public static MemPoolInfo GenerateMempoolInfo()
	{
		var histogram = GenerateHistogram().ToArray();
		var totalSize = (ulong)histogram.Sum(x => (decimal)x.Sizes);
		var txCount = (ulong)histogram.Sum(x => (decimal)x.Count);
		var isMemPoolAlmostFull = totalSize > 250_000_000;

		return new MemPoolInfo
		{
			Histogram = histogram,
			MemPoolMinFee = isMemPoolAlmostFull ? 0.00004000 : 0.00001000,
			Bytes = (int)totalSize,
			Size = (int)txCount,
			MinRelayTxFee = 0.00001000,
			MaxMemPool = 300_000_000
		};
	}

	private static IEnumerable<FeeRateGroup> GenerateHistogram()
		=> FeeRanges.Select(x => GenerateFeeRateGroup(x.from, x.to));

	private static FeeRateGroup GenerateFeeRateGroup(int from, int to)
	{
		var count = Math.Max(1, Random.Shared.Gaussian(10_000 - 5 * to, 1_000));
		var sizes = count * Math.Max(250, Random.Shared.Gaussian(500, 100));
		return new FeeRateGroup
		{
			Count = (uint)count,
			Sizes = (uint)sizes,
			From = new FeeRate((decimal)from),
			To = new FeeRate((decimal)to),
			Fees = Money.Satoshis((decimal)((to - from) / 2.0 * sizes)),
			Group = from
		};
	}
}
