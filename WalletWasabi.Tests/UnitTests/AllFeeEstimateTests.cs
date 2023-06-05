using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;
using Moq;
using System.Threading;
using WalletWasabi.Extensions;

namespace WalletWasabi.Tests.UnitTests;

public class AllFeeEstimateTests
{
	[Fact]
	public void Serialization()
	{
		var estimations = new Dictionary<int, int>
			{
				{ 2, 102 },
				{ 3, 20 },
				{ 19, 1 }
			};
		var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		var serialized = JsonConvert.SerializeObject(allFee);
		var deserialized = JsonConvert.DeserializeObject<AllFeeEstimate>(serialized);

		Assert.NotNull(deserialized);
		Assert.Equal(estimations[2], deserialized!.Estimations[2]);
		Assert.Equal(estimations[3], deserialized!.Estimations[3]);
		Assert.Equal(estimations[19], deserialized!.Estimations[36]);
		Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized!.Type);
	}

	[Fact]
	public void OrdersByTarget()
	{
		var estimations = new Dictionary<int, int>
			{
				{ 3, 20 },
				{ 2, 102 },
				{ 19, 1 },
				{ 20, 1 }
			};

		var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
		Assert.Equal(estimations[3], allFee.Estimations[3]);
		Assert.Equal(estimations[19], allFee.Estimations[36]);
	}

	[Fact]
	public void HandlesDuplicate()
	{
		var estimations = new Dictionary<int, int>
			{
				{ 2, 20 },
				{ 3, 20 }
			};

		var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		Assert.Single(allFee.Estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
	}

	[Fact]
	public void HandlesOne()
	{
		// If there's no 2, this'll be 2.
		var estimations = new Dictionary<int, int>
			{
				{ 1, 20 }
			};

		var allFees = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		Assert.Single(allFees.Estimations);
		Assert.Equal(estimations[1], allFees.Estimations[2]);

		// If there's 2, 1 is dismissed.
		estimations = new Dictionary<int, int>
			{
				{ 1, 20 },
				{ 2, 21 }
			};

		allFees = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		Assert.Single(allFees.Estimations);
		Assert.Equal(estimations[2], allFees.Estimations[2]);
	}

	[Fact]
	public void EndOfTheRange()
	{
		var estimations = new Dictionary<int, int>
			{
				{ 1007, 20 }
			};

		var allFees = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		var est = Assert.Single(allFees.Estimations);
		Assert.Equal(1008, est.Key);
	}

	[Fact]
	public void HandlesInconsistentData()
	{
		var estimations = new Dictionary<int, int>
			{
				{ 2, 20 },
				{ 3, 21 }
			};

		var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		Assert.Single(allFee.Estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);

		estimations = new Dictionary<int, int>
			{
				{ 18, 1000 },
				{ 3, 21 },
				{ 2, 20 },
				{ 100, 100 },
				{ 6, 4 },
			};

		allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
		Assert.Equal(2, allFee.Estimations.Count);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
		Assert.Equal(estimations[6], allFee.Estimations[6]);
	}

	[Fact]
	public async Task RpcNotEnoughEstimationsAsync()
	{
		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new BlockchainInfo
			{
				Blocks = 100,
				Headers = 100
			});
		mockRpc.Setup(rpc => rpc.GetPeersInfoAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(Array.Empty<PeerInfo>());
		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new NoEstimationException(1));
		mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);

		await Assert.ThrowsAsync<NoEstimationException>(async () => await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
	}

	[Fact]
	public async Task RpcFailuresAsync()
	{
		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-GetBlockchainInfo", null));

		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-EstimateSmartFee", null));

		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});

		mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);

		var ex = await Assert.ThrowsAsync<RPCException>(async () => await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
		Assert.Equal(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, ex.RPCCode);
		Assert.Equal("Error-EstimateSmartFee", ex.Message);
	}

	[Fact]
	public async Task ToleratesRpcFailuresAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient();
		var any = EstimateSmartFeeMode.Conservative;
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(2, 100m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any, It.IsAny<CancellationToken>())).ThrowsAsync(new RPCException(RPCErrorCode.RPC_INTERNAL_ERROR, "Error", null));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(5, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(5, 89m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(6, 75m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(8, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(8, 70m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn(2, 3, 5, 6, 8), any, It.IsAny<CancellationToken>())).ThrowsAsync(new NoEstimationException(0));

		var allFee = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
		Assert.Equal(2, allFee.Estimations.Count);
		Assert.False(allFee.Estimations.ContainsKey(3));
		Assert.False(allFee.Estimations.ContainsKey(5));
		Assert.False(allFee.Estimations.ContainsKey(8));
	}

	[Fact]
	public async Task AccurateEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var any = EstimateSmartFeeMode.Conservative;

		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(2, 99m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(3, 99m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(5, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(5, 89m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(6, 75m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(8, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(8, 30m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(11, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(11, 30m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(13, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(13, 30m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(15, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(15, 30m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(1008, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(1008, 31m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn(2, 3, 5, 6, 8, 11, 13, 15, 1008), any, It.IsAny<CancellationToken>())).ThrowsAsync(new NoEstimationException(0));

		var allFee = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
		Assert.True(allFee.IsAccurate);
		Assert.Equal(3, allFee.Estimations.Count);
		Assert.Equal(99, allFee.Estimations[2]);
		Assert.Equal(75, allFee.Estimations[6]);
		Assert.Equal(31, allFee.Estimations[1008]);
	}

	[Fact]
	public async Task FixObviouslyWrongEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var any = EstimateSmartFeeMode.Conservative;

		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
			new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000, // 1 s/b (default value)
				Histogram = MempoolInfoGenerator.FeeRanges.Select((x, i) => new FeeRateGroup
				{
					Count = (uint)(200 * (i + 1)),
					Sizes = (uint)(40 * 100 * (i + 1)),
					From = new FeeRate((decimal)x.from),
					To = new FeeRate((decimal)x.to),
					Fees = Money.Zero,
					Group = x.from
				}).ToArray()
			});
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(2, 3_500m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(3, 500m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(6, 10m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(18, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(18, 5m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(36, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(36, 5m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(1008, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(1008, 1m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn(2, 3, 5, 6, 8, 11, 13, 15, 1008), any, It.IsAny<CancellationToken>())).ThrowsAsync(new NoEstimationException(0));

		var allFee = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
		Assert.Equal(3_500, allFee.Estimations[2]);
		Assert.True(allFee.Estimations[3] > 500);
		Assert.True(allFee.Estimations[1008] > 1);
	}

	[Fact]
	public async Task WorksWithBitcoinCoreEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var any = EstimateSmartFeeMode.Conservative;

		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(2, 120m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn(2), any, It.IsAny<CancellationToken>())).ThrowsAsync(new NoEstimationException(0));

		// Do not throw exception
		await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
	}

	[Fact]
	public async Task ExhaustiveMempoolEstimationsAsync()
	{
		foreach (var _ in Enumerable.Range(0, 100))
		{
			var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
			var mempoolInfo = MempoolInfoGenerator.GenerateMempoolInfo();
			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mempoolInfo);
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), EstimateSmartFeeMode.Conservative, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(2, 120m));
			var feeRates = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			var estimations = feeRates.Estimations;

			Assert.Subset(Constants.ConfirmationTargets.ToHashSet(), estimations.Keys.ToHashSet());
			Assert.Equal(estimations.Keys, estimations.Keys.OrderBy(x => x));
			Assert.Equal(estimations.Values, estimations.Values.OrderByDescending(x => x));
			Assert.All(estimations, (e) => Assert.True(e.Value >= mempoolInfo.MemPoolMinFee * 100_000));
		}
	}

	[Fact]
	public async Task RealWorldMempoolSpaceMinFeeAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var mempoolInfo = MempoolInfoGenerator.GenerateRealMempoolInfo();
		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mempoolInfo);
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), EstimateSmartFeeMode.Conservative, It.IsAny<CancellationToken>()))
			.ReturnsAsync(FeeRateResponse(2, 0m)); // all estimations a 0 s/b so, only estimations based on mempool.
		var feeRates = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
		var estimations = feeRates.Estimations;
		var minFee = estimations.Min(x => x.Value);

		Assert.Equal(15, minFee); // this is the calculated MempoolMinFee needed to be in the top 200MB
	}

	[Theory]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram1.json", 2)]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram2.json", 12)]
	public async Task RealWorldMempoolRpcMinFeeAsync(string filePath, int expectedMinFee)
	{
		// This test is for making sure we don't under fee.
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var mempoolInfo = MempoolInfoGenerator.GenerateRealBitcoinKnotsMemPoolInfo(filePath);
		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mempoolInfo);
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), EstimateSmartFeeMode.Conservative, It.IsAny<CancellationToken>()))
			.ReturnsAsync(FeeRateResponse(2, 0m)); // all estimations a 0 s/b so, only estimations based on mempool.
		var feeRates = await mockRpc.Object.EstimateAllFeeAsync();
		var estimations = feeRates.Estimations;
		var minFee = estimations.Min(x => x.Value);

		Assert.Equal(expectedMinFee, minFee);
	}

	[Theory]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram1.json", 100)]
	[InlineData("./UnitTests/Data/MempoolInfoWithHistogram2.json", 50)]
	public async Task RealWorldMempoolRpcMaxFeeAsync(string filePath, int expectedMaxFee)
	{
		// This test is for making sure we don't overpay fee.
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		var mempoolInfo = MempoolInfoGenerator.GenerateRealBitcoinKnotsMemPoolInfo(filePath);
		var any = EstimateSmartFeeMode.Conservative;
		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mempoolInfo);
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(2, 1_000m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(5, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(5, 89m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(18, 75m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(8, any, It.IsAny<CancellationToken>())).ReturnsAsync(FeeRateResponse(144, 10m));
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn(2, 5, 6, 8), any, It.IsAny<CancellationToken>())).ThrowsAsync(new NoEstimationException(0));

		var feeRates = await mockRpc.Object.EstimateAllFeeAsync();
		var estimations = feeRates.Estimations;
		var maxFee = estimations.Max(x => x.Value);

		Assert.Equal(expectedMaxFee, maxFee);
	}

	private static Mock<IRPCClient> CreateAndConfigureRpcClient(bool isSynchronized = true, bool hasPeersInfo = false, double memPoolMinFee = 0.00001000)
	{
		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
			new BlockchainInfo
			{
				Blocks = isSynchronized ? 100_000UL : 89_765UL,
				Headers = 100_000L
			});
		mockRpc.Setup(rpc => rpc.GetPeersInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
			hasPeersInfo
				? new[] { new PeerInfo() }
				: Array.Empty<PeerInfo>());
		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
			new MemPoolInfo
			{
				MemPoolMinFee = memPoolMinFee, // 1 s/b (default value)
				Histogram = Array.Empty<FeeRateGroup>()
			});
		mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);

		return mockRpc;
	}

	private static EstimateSmartFeeResponse FeeRateResponse(int target, decimal feeRate) =>
		new()
		{
			Blocks = target,
			FeeRate = new FeeRate(feeRate)
		};
}
