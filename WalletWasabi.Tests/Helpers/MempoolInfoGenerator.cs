using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.Tests.Helpers;

public class MempoolInfoGenerator
{
	private static readonly int[] FeeLimits =
	{
		1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 17, 20, 25, 30, 40, 50, 60, 70, 80,
		100, 120, 140, 170, 200, 250, 300, 400, 500, 600, 700, 800, 1000,
		1200, 1400, 1700, 2000, 2500, 3000, 4000, 5000, 6000, 7000, 8000, 10000
	};

	public static (int from, int to)[] FeeRanges { get; } = FeeLimits.Zip(FeeLimits.Skip(1), (from, to) => (from, to)).ToArray();

	public static MemPoolInfo GenerateRealMempoolInfo()
	{
		var mempoolInfoFeeLimits = new[]
		{
			// The fee scale used by mempool.space
			1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30, 40, 50, 60, 70, 80, 100, 125,
			150, 175, 200, 250, 300, 350, 400, 500, 600, 700, 800, 900, 1000
		};

		var realSizesHistogram = new[]
		{
			// Extracted from mempool.space (May 8th)
			26912900, 39041211, 11497886, 12630137, 5215539, 5346874, 4165246, 6182007, 22028761, 10532242,
			7840968, 4766683, 5323740, 3866718, 4380467, 3821845, 2669088, 1594671, 3365674, 3032493,
			3489014, 2257412, 5913665, 798395, 457517, 251187, 392587, 540874, 132731, 36160, 11657, 6884,
			4554, 1066, 244, 116, 664, 1056
		};

		static FeeRateGroup ToFeeRateGroup((int from, int to) range, int size)
		{
			var count = size / Math.Max(300, Random.Shared.Gaussian(500, 100));
			var avgFeeRate = range.from + (range.to - range.from) / 2.0m;
			return new FeeRateGroup
			{
				Count = (uint)count,
				Sizes = (uint)size,
				From = new FeeRate((decimal)range.from),
				To = new FeeRate((decimal)range.to),
				Fees = Money.Satoshis(avgFeeRate * size),
				Group = range.from
			};
		}

		var histogram = mempoolInfoFeeLimits
			.Zip(mempoolInfoFeeLimits.Skip(1), (from, to) => (from, to))
			.Zip(realSizesHistogram)
			.Select(x => ToFeeRateGroup(x.First, x.Second))
			.ToArray();

		var totalSize = (ulong)histogram.Sum(x => (decimal)x.Sizes);
		var txCount = (ulong)histogram.Sum(x => (decimal)x.Count);

		return new MemPoolInfo
		{
			Histogram = histogram,
			MemPoolMinFee = 0.00001000,
			Bytes = (int)totalSize,
			Size = (int)txCount,
			MinRelayTxFee = 0.00001000,
			MaxMemPool = 1_000_000_000
		};
	}

	public static MemPoolInfo GenerateRealBitcoinKnotsMemPoolInfo(string filePath)
	{
		var response = new RPCResponse(
			(JObject)JsonConvert.DeserializeObject(File.ReadAllText(filePath))!);
		static IEnumerable<FeeRateGroup> ExtractFeeRateGroups(JToken? jt) =>
			jt switch
			{
				JObject jo => jo.Properties()
					.Where(p => p.Name != "total_fees")
					.Select(
						p =>
						{
							var rawToFeeRate = p.Value.Value<ulong>("to_feerate");
							var toFeeRate = (decimal)Math.Min(rawToFeeRate, 5_000);

							return new FeeRateGroup
							{
								Group = int.Parse(p.Name),
								Sizes = p.Value.Value<ulong>("sizes"),
								Count = p.Value.Value<uint>("count"),
								Fees = Money.Satoshis(p.Value.Value<ulong>("fees")),
								From = new FeeRate(p.Value.Value<decimal>("from_feerate")),
								To = new FeeRate(toFeeRate)
							};
						}),
				_ => Enumerable.Empty<FeeRateGroup>()
			};

		var info = response.Result;
		return new MemPoolInfo()
		{
			Size = info.Value<int>("size"),
			Bytes = info.Value<int>("bytes"),
			Usage = info.Value<int>("usage"),
			MaxMemPool = info.Value<double>("maxmempool"),
			MemPoolMinFee = info.Value<double>("mempoolminfee"),
			MinRelayTxFee = info.Value<double>("minrelaytxfee"),
			Histogram = ExtractFeeRateGroups(info["fee_histogram"] ).ToArray()
		};
	}

	public static MemPoolInfo GenerateMempoolInfo()
	{
		var histogram = GenerateHistogram().ToArray();
		var totalSize = (ulong)histogram.Sum(x => (decimal)x.Sizes);
		var txCount = (ulong)histogram.Sum(x => (decimal)x.Count);

		return new MemPoolInfo
		{
			Histogram = histogram,
			MemPoolMinFee = 0.00001000,
			Bytes = (int)totalSize,
			Size = (int)txCount,
			MinRelayTxFee = 0.00001000,
			MaxMemPool = 1_000_000_000
		};
	}

	private static IEnumerable<FeeRateGroup> GenerateHistogram()
		=> FeeRanges.Select(x => GenerateFeeRateGroup(x.from, x.to));

	private static FeeRateGroup GenerateFeeRateGroup(int from, int to)
	{
		var count = 100_000 / (to - from);
		var sizes = count * Math.Max(300, Random.Shared.Gaussian(500, 100));
		var avgFeeRate = from + ((to - from) / 2.0m);
		return new FeeRateGroup
		{
			Count = (uint)count,
			Sizes = (uint)sizes,
			From = new FeeRate((decimal)from),
			To = new FeeRate((decimal)to),
			Fees = Money.Satoshis(avgFeeRate * (decimal)sizes),
			Group = from
		};
	}
}
