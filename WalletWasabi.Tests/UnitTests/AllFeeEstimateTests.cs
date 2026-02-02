using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

/// <summary>
/// Tests for <see cref="AllFeeEstimate"/>.
/// </summary>
public class AllFeeEstimateTests
{
	[Fact]
	public void OrdersByTarget()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 3, new FeeRate( 20M) },
			{ 2, new FeeRate( 102M) },
			{ 19,new FeeRate(  1M) },
			{ 20,new FeeRate(  1M) }
		};

		var allFee = new AllFeeEstimate(estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
		Assert.Equal(estimations[3], allFee.Estimations[3]);
		Assert.Equal(estimations[19], allFee.Estimations[36]);
	}

	[Fact]
	public void HandlesDuplicate()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 2, new FeeRate(20M) },
			{ 3, new FeeRate(20M) }
		};

		var allFee = new AllFeeEstimate(estimations);
		Assert.Single(allFee.Estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
	}

	[Fact]
	public void HandlesOne()
	{
		// If there's no 2, this'll be 2.
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 1, new FeeRate(20M) }
		};

		var allFees = new AllFeeEstimate(estimations);
		Assert.Single(allFees.Estimations);
		Assert.Equal(estimations[1], allFees.Estimations[2]);

		// If there's 2, 1 is dismissed.
		estimations = new Dictionary<int, FeeRate>
		{
			{ 1, new FeeRate(20M) },
			{ 2, new FeeRate(21M) }
		};

		allFees = new AllFeeEstimate(estimations);
		Assert.Single(allFees.Estimations);
		Assert.Equal(estimations[2], allFees.Estimations[2]);
	}

	[Fact]
	public void EndOfTheRange()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 1007, new FeeRate(20M) }
		};

		var allFees = new AllFeeEstimate(estimations);
		var est = Assert.Single(allFees.Estimations);
		Assert.Equal(1008, est.Key);
	}

	[Fact]
	public void HandlesInconsistentData()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 2, new FeeRate(20M) },
			{ 3, new FeeRate(21M) }
		};

		var allFee = new AllFeeEstimate(estimations);
		Assert.Single(allFee.Estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);

		estimations = new Dictionary<int, FeeRate>
		{
			{ 18, new FeeRate(1000M) },
			{ 3, new FeeRate(21M) },
			{ 2, new FeeRate(20M) },
			{ 100, new FeeRate(100M) },
			{ 6, new FeeRate(4M) },
		};

		allFee = new AllFeeEstimate(estimations);
		Assert.Equal(2, allFee.Estimations.Count);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
		Assert.Equal(estimations[6], allFee.Estimations[6]);
	}

	[Fact]
	public async Task RpcNotEnoughEstimationsAsync()
	{
		var mockRpc = new MockRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromResult(new BlockchainInfo
			{
				Blocks = 100,
				Headers = 100
			});
		mockRpc.OnGetPeersInfoAsync = () =>
			Task.FromResult(Array.Empty<PeerInfo>());
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});
		mockRpc.OnEstimateSmartFeeAsync = (_, _) =>
			throw new NoEstimationException(1);

		await Assert.ThrowsAsync<NoEstimationException>(async () => await mockRpc.EstimateAllFeeAsync());
	}

	[Fact]
	public async Task RpcFailuresAsync()
	{
		var mockRpc = new MockRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromException<BlockchainInfo>(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-GetBlockchainInfo", null));

		mockRpc.OnEstimateSmartFeeAsync = (_, _) =>
			Task.FromException<EstimateSmartFeeResponse>(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-EstimateSmartFee", null));

		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});

		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(500));
		var ex = await Assert.ThrowsAsync<RPCException>(async () => await mockRpc.EstimateAllFeeAsync());
		Assert.Equal(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, ex.RPCCode);
		Assert.Equal("Error-EstimateSmartFee", ex.Message);
	}

	[Fact]
	public async Task ToleratesRpcFailuresAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 100m)),
				3 => Task.FromException<EstimateSmartFeeResponse>(new RPCException(RPCErrorCode.RPC_INTERNAL_ERROR, "Error", null)),
				5 => Task.FromResult(FeeRateResponse(5, 89m)),
				6 => Task.FromResult(FeeRateResponse(6, 75m)),
				8 => Task.FromResult(FeeRateResponse(8, 70m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var allFee = await mockRpc.EstimateAllFeeAsync();
		Assert.Equal(2, allFee.Estimations.Count);
		Assert.False(allFee.Estimations.ContainsKey(3));
		Assert.False(allFee.Estimations.ContainsKey(5));
		Assert.False(allFee.Estimations.ContainsKey(8));
	}

	[Fact]
	public async Task AccurateEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		mockRpc.Network = Network.Main;
		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 99m)),
				3 => Task.FromResult(FeeRateResponse(3, 99m)),
				5 => Task.FromResult(FeeRateResponse(5, 89m)),
				6 => Task.FromResult(FeeRateResponse(6, 75m)),
				8 => Task.FromResult(FeeRateResponse(8, 30m)),
				11 => Task.FromResult(FeeRateResponse(11, 30m)),
				13 => Task.FromResult(FeeRateResponse(13, 30m)),
				15 => Task.FromResult(FeeRateResponse(15, 30m)),
				1008 => Task.FromResult(FeeRateResponse(1008, 31m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var allFee = await mockRpc.EstimateAllFeeAsync();
		Assert.Equal(3, allFee.Estimations.Count);
		Assert.Equal(99, allFee.Estimations[2].SatoshiPerByte);
		Assert.Equal(75, allFee.Estimations[6].SatoshiPerByte);
		Assert.Equal(31, allFee.Estimations[1008].SatoshiPerByte);
	}

	[Fact]
	public async Task FixObviouslyWrongEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);

		var histogram = MempoolInfoGenerator.FeeRanges.Reverse().Select((x, i) => new FeeRateGroup
		{
			Count = (uint) (100 * Math.Pow(i + 1, 2)),
			Sizes = (uint) (40 * 100 * (i + 1)),
			From = new FeeRate((decimal) x.from),
			To = new FeeRate((decimal) x.to),
			Fees = Money.Zero,
			Group = x.from
		}).ToArray();

		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000, // 1 s/b (default value)
				Histogram = histogram,
				Size = (int)histogram.Sum(x => x.Count)
			});

		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 3_500m)),
				3 => Task.FromResult(FeeRateResponse(3, 500m)),
				6 => Task.FromResult(FeeRateResponse(6, 10m)),
				18 => Task.FromResult(FeeRateResponse(18, 5m)),
				36 => Task.FromResult(FeeRateResponse(36, 5m)),
				1008 => Task.FromResult(FeeRateResponse(1008, 1m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var allFee = await mockRpc.EstimateAllFeeAsync();
		Assert.Equal(140, allFee.Estimations[2].SatoshiPerByte);
		Assert.Equal(124.428m, allFee.Estimations[144].SatoshiPerByte);
		Assert.True(allFee.Estimations[1008].SatoshiPerByte > 1);
	}


	[Fact]
	public async Task WorksWithBitcoinCoreEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);

		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 120m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		// Do not throw exception
		await mockRpc.EstimateAllFeeAsync();
	}

	[Fact]
	public async Task ExhaustiveMempoolEstimationsAsync()
	{
		foreach (var _ in Enumerable.Range(0, 100))
		{
			var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
			var mempoolInfo = MempoolInfoGenerator.GenerateMempoolInfo();
			mockRpc.OnGetMempoolInfoAsync = () => Task.FromResult(mempoolInfo);
			mockRpc.OnEstimateSmartFeeAsync = (_, _) => Task.FromResult(FeeRateResponse(2, 120m));
			var feeRates = await mockRpc.EstimateAllFeeAsync();
			var estimations = feeRates.Estimations;

			Assert.Subset(Constants.ConfirmationTargets.ToHashSet(), estimations.Keys.ToHashSet());
			Assert.Equal(estimations.Keys, estimations.Keys.OrderBy(x => x));
			Assert.Equal(estimations.Values, estimations.Values.OrderByDescending(x => x));
			Assert.All(estimations, (e) => Assert.True(e.Value.SatoshiPerByte >= (decimal)mempoolInfo.MemPoolMinFee * 100_000));
		}
	}

	[Fact]
	public async Task RealWorldMempoolSpaceMinFeeAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var mempoolInfo = MempoolInfoGenerator.GenerateRealMempoolInfo();
		mockRpc.OnGetMempoolInfoAsync = () => Task.FromResult(mempoolInfo);
		mockRpc.OnEstimateSmartFeeAsync = (_, _) => Task.FromResult(FeeRateResponse(2, 0m));
		var feeRates = await mockRpc.EstimateAllFeeAsync();
		var estimations = feeRates.Estimations;
		var minFee = estimations.Min(x => x.Value)!;

		Assert.NotNull(minFee);
		Assert.Equal(15, minFee.SatoshiPerByte); // this is the calculated MempoolMinFee needed to be in the top 200MB
	}

	[Theory]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram1.json", 2)]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram2.json", 12)]
	public async Task RealWorldMempoolRpcMinFeeAsync(string filePath, int expectedMinFee)
	{
		// This test is for making sure we don't underpay the network fee.
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var mempoolInfo = MempoolInfoGenerator.GenerateRealBitcoinKnotsMemPoolInfo(filePath);
		mockRpc.OnGetMempoolInfoAsync = () => Task.FromResult(mempoolInfo);
		mockRpc.OnEstimateSmartFeeAsync = (_, _) => Task.FromResult(FeeRateResponse(2, 0m));
		var feeRates = await mockRpc.EstimateAllFeeAsync();
		var estimations = feeRates.Estimations;
		var minFee = estimations.Min(x => x.Value)!;

		Assert.NotNull(minFee);
		Assert.Equal(expectedMinFee, minFee.SatoshiPerByte);
	}

	[Theory]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram1.json", 100)]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram2.json", 50)]
	public async Task RealWorldMempoolRpcMaxFeeAsync(string filePath, int expectedMaxFee)
	{
		// This test is for making sure we don't overpay the network fee.
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var mempoolInfo = MempoolInfoGenerator.GenerateRealBitcoinKnotsMemPoolInfo(filePath);

		mockRpc.OnGetMempoolInfoAsync = () => Task.FromResult(mempoolInfo);
		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 1_000m)),
				5 => Task.FromResult(FeeRateResponse(5, 89m)),
				6 => Task.FromResult(FeeRateResponse(18, 75m)),
				8 => Task.FromResult(FeeRateResponse(144, 10m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var feeRates = await mockRpc.EstimateAllFeeAsync();
		var estimations = feeRates.Estimations;
		var maxFee = estimations.Max(x => x.Value)!;

		Assert.NotNull(maxFee);
		Assert.Equal(expectedMaxFee, maxFee.SatoshiPerByte);
	}

	[Fact]
	public void WildEstimations()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 2,new FeeRate(102m) }, // 20m
			{ 3,new FeeRate(20m) }, // 30m
			{ 6,new FeeRate(10m) }, // 1h
			{ 18,new FeeRate(1m) } // 3h
		};

		var allFee = new AllFeeEstimate(estimations);

		Assert.Equal(7, allFee.WildEstimations.Count);
		Assert.Equal(new FeeRate(102m), allFee.WildEstimations[0].feeRate); // 20m
		Assert.Equal(new FeeRate(20m), allFee.WildEstimations[1].feeRate); // 30m
		Assert.Equal(new FeeRate(16.666m), allFee.WildEstimations[2].feeRate); // 40m
		Assert.Equal(new FeeRate(13.333m), allFee.WildEstimations[3].feeRate); // 50m
		Assert.Equal(new FeeRate(10m), allFee.WildEstimations[4].feeRate); // 1h
		Assert.Equal(new FeeRate(5.5m), allFee.WildEstimations[5].feeRate); // 2h
		Assert.Equal(new FeeRate(1m), allFee.WildEstimations[6].feeRate); // 3h

		Assert.Equal(TimeSpan.FromMinutes(10), allFee.EstimateConfirmationTime(new FeeRate(200m)));
		Assert.Equal(TimeSpan.FromMinutes(20), allFee.EstimateConfirmationTime(new FeeRate(102.1m)));
		Assert.Equal(TimeSpan.FromMinutes(20), allFee.EstimateConfirmationTime(new FeeRate(102m)));
		Assert.Equal(TimeSpan.FromMinutes(30), allFee.EstimateConfirmationTime(new FeeRate(101.9m)));
		Assert.Equal(TimeSpan.FromMinutes(30), allFee.EstimateConfirmationTime(new FeeRate(50m)));
		Assert.Equal(TimeSpan.FromMinutes(30), allFee.EstimateConfirmationTime(new FeeRate(20m)));
		Assert.Equal(TimeSpan.FromMinutes(40), allFee.EstimateConfirmationTime(new FeeRate(19m)));
		Assert.Equal(TimeSpan.FromMinutes(60), allFee.EstimateConfirmationTime(new FeeRate(11m)));
		Assert.Equal(TimeSpan.FromMinutes(60), allFee.EstimateConfirmationTime(new FeeRate(10m)));
		Assert.Equal(TimeSpan.FromHours(2), allFee.EstimateConfirmationTime(new FeeRate(9m)));
		Assert.Equal(TimeSpan.FromHours(3), allFee.EstimateConfirmationTime(new FeeRate(3m)));
		Assert.Equal(TimeSpan.FromHours(3), allFee.EstimateConfirmationTime(new FeeRate(1m)));
		Assert.Equal(TimeSpan.FromHours(3), allFee.EstimateConfirmationTime(new FeeRate(0.1m)));
	}

	private static MockRpcClient CreateAndConfigureRpcClient(bool isSynchronized = true, bool hasPeersInfo = false, double memPoolMinFee = 0.00001000)
	{
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromResult(new BlockchainInfo
			{
				Blocks = isSynchronized ? 100_000UL : 89_765UL,
				Headers = 100_000L
			});
		mockRpc.OnGetPeersInfoAsync = () =>
			Task.FromResult(hasPeersInfo
				? new[] { new PeerInfo() }
				: Array.Empty<PeerInfo>());
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = memPoolMinFee, // 1 s/b (default value)
				Histogram = Array.Empty<FeeRateGroup>()
			});

		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(500));
		return mockRpc;
	}

	private static EstimateSmartFeeResponse FeeRateResponse(int target, decimal feeRate) =>
		new()
		{
			Blocks = target,
			FeeRate = new FeeRate(feeRate)
		};
}
