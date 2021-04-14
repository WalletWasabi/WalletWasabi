using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.BitcoinCore.Rpc;
using Newtonsoft.Json.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;
using Moq;

namespace WalletWasabi.Tests.UnitTests
{
	public class BestFeeEstimatesTests
	{
		[Fact]
		public void BestFeeEstimatesSerialization()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 102 },
				{ 3, 20 },
				{ 19, 1 }
			};
			var bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			var serialized = JsonConvert.SerializeObject(bestFees);
			var deserialized = JsonConvert.DeserializeObject<BestFeeEstimates>(serialized);

			Assert.Equal(estimations[2], deserialized.Estimations[2]);
			Assert.Equal(estimations[3], deserialized.Estimations[3]);
			Assert.Equal(estimations[19], deserialized.Estimations[36]);
			Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized.Type);
		}

		[Fact]
		public void BestFeeEstimatesOrdersByTarget()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 3, 20 },
				{ 2, 102 },
				{ 19, 1 },
				{ 20, 1 }
			};

			var bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(estimations[2], bestFees.Estimations[2]);
			Assert.Equal(estimations[3], bestFees.Estimations[3]);
			Assert.Equal(estimations[19], bestFees.Estimations[36]);
		}

		[Fact]
		public void BestFeeEstimatesHandlesDuplicate()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 20 },
				{ 3, 20 }
			};

			var bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(bestFees.Estimations);
			Assert.Equal(estimations[2], bestFees.Estimations[2]);
		}

		[Fact]
		public void BestFeeEstimatesHandlesOne()
		{
			// If there's no 2, this'll be 2.
			var estimations = new Dictionary<int, int>
			{
				{ 1, 20 }
			};

			var bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(bestFees.Estimations);
			Assert.Equal(estimations[1], bestFees.Estimations[2]);

			// If there's 2, 1 is dismissed.
			estimations = new Dictionary<int, int>
			{
				{ 1, 20 },
				{ 2, 21 }
			};

			bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(bestFees.Estimations);
			Assert.Equal(estimations[2], bestFees.Estimations[2]);
		}

		[Fact]
		public void EndOfTheRange()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 1007, 20 }
			};

			var bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			var est = Assert.Single(bestFees.Estimations);
			Assert.Equal(1008, est.Key);
		}

		[Fact]
		public void BestFeeEstimatesHandlesInconsistentData()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 20 },
				{ 3, 21 }
			};

			var bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(bestFees.Estimations);
			Assert.Equal(estimations[2], bestFees.Estimations[2]);

			estimations = new Dictionary<int, int>
			{
				{ 18, 1000 },
				{ 3, 21 },
				{ 2, 20 },
				{ 100, 100 },
				{ 6, 4 },
			};

			bestFees = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(2, bestFees.Estimations.Count);
			Assert.Equal(estimations[2], bestFees.Estimations[2]);
			Assert.Equal(estimations[6], bestFees.Estimations[6]);
		}

		[Fact]
		public async Task RpcNotEnoughEstimationsAsync()
		{
			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync())
				.ReturnsAsync(new BlockchainInfo
				{
					Blocks = 100,
					Headers = 100
				});
			mockRpc.Setup(rpc => rpc.GetPeersInfoAsync())
				.ReturnsAsync(Array.Empty<PeerInfo>());
			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync())
				.ReturnsAsync(new MemPoolInfo
				{
					MemPoolMinFee = 0.00001000 // 1 s/b (default value)
				});
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>()))
				.ThrowsAsync(new NoEstimationException(1));
			mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);

			await Assert.ThrowsAsync<NoEstimationException>(async () => await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative));
		}

		[Fact]
		public async Task RpcFailuresAsync()
		{
			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync())
				.ThrowsAsync(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-GetBlockchainInfo", null));

			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>()))
				.ThrowsAsync(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-EstimateSmartFee", null));

			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync())
				.ReturnsAsync(new MemPoolInfo
				{
					MemPoolMinFee = 0.00001000 // 1 s/b (default value)
				});

			mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);

			var ex = await Assert.ThrowsAsync<RPCException>(async () => await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative));
			Assert.Equal(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, ex.RPCCode);
			Assert.Equal("Error-EstimateSmartFee", ex.Message);
		}

		[Fact]
		public async Task ToleratesRpcFailuresAsync()
		{
			var mockRpc = CreateAndConfigureRpcClient();
			var any = EstimateSmartFeeMode.Conservative;
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any)).ReturnsAsync(FeeRateResponse(2, 100m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any)).ThrowsAsync(new RPCException(RPCErrorCode.RPC_INTERNAL_ERROR, "Error", null));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(5, any)).ReturnsAsync(FeeRateResponse(5, 89m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any)).ReturnsAsync(FeeRateResponse(6, 75m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(8, any)).ReturnsAsync(FeeRateResponse(8, 70m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn<int>(2, 3, 5, 6, 8), any)).ThrowsAsync(new NoEstimationException(0));

			var bestFees = await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative);
			Assert.Equal(2, bestFees.Estimations.Count);
			Assert.False(bestFees.Estimations.ContainsKey(3));
			Assert.False(bestFees.Estimations.ContainsKey(5));
			Assert.False(bestFees.Estimations.ContainsKey(8));
		}

		[Fact]
		public async Task AccurateEstimationsAsync()
		{
			var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
			var any = EstimateSmartFeeMode.Conservative;

			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any)).ReturnsAsync(FeeRateResponse(2, 99m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any)).ReturnsAsync(FeeRateResponse(3, 99m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(5, any)).ReturnsAsync(FeeRateResponse(5, 89m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any)).ReturnsAsync(FeeRateResponse(6, 75m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(8, any)).ReturnsAsync(FeeRateResponse(8, 30m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(11, any)).ReturnsAsync(FeeRateResponse(11, 30m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(13, any)).ReturnsAsync(FeeRateResponse(13, 30m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(15, any)).ReturnsAsync(FeeRateResponse(15, 30m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(1008, any)).ReturnsAsync(FeeRateResponse(1008, 31m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn<int>(2, 3, 5, 6, 8, 11, 13, 15, 1008), any)).ThrowsAsync(new NoEstimationException(0));

			var bestFees = await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative);
			Assert.True(bestFees.IsAccurate);
			Assert.Equal(3, bestFees.Estimations.Count);
			Assert.Equal(99, bestFees.Estimations[2]);
			Assert.Equal(75, bestFees.Estimations[6]);
			Assert.Equal(31, bestFees.Estimations[1008]);
		}

		[Fact]
		public async Task FixObviouslyWrongEstimationsAsync()
		{
			var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
			var any = EstimateSmartFeeMode.Conservative;

			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync()).ReturnsAsync(
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
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any)).ReturnsAsync(FeeRateResponse(2, 3_500m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any)).ReturnsAsync(FeeRateResponse(3, 500m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any)).ReturnsAsync(FeeRateResponse(6, 10m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(18, any)).ReturnsAsync(FeeRateResponse(18, 5m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(36, any)).ReturnsAsync(FeeRateResponse(36, 5m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(1008, any)).ReturnsAsync(FeeRateResponse(1008, 1m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn<int>(2, 3, 5, 6, 8, 11, 13, 15, 1008), any)).ThrowsAsync(new NoEstimationException(0));

			var bestFees = await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative);
			Assert.Equal(3_500, bestFees.Estimations[2]);
			Assert.True(bestFees.Estimations[3] > 500);
			Assert.True(bestFees.Estimations[1008] > 1);
		}

		[Fact]
		public async Task WorksWithBitcoinCoreEstimationsAsync()
		{
			var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
			var any = EstimateSmartFeeMode.Conservative;

			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any)).ReturnsAsync(FeeRateResponse(2, 120m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn<int>(2), any)).ThrowsAsync(new NoEstimationException(0));

			// Do not throw exception
			await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative);
		}

		[Fact]
		public async Task ExhaustiveMempoolEstimationsAsync()
		{
			foreach (var i in Enumerable.Range(0, 1000))
			{
				var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
				var mempoolInfo = MempoolInfoGenerator.GenerateMempoolInfo();
				mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync()).ReturnsAsync(mempoolInfo);
				mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), EstimateSmartFeeMode.Conservative)).ReturnsAsync(FeeRateResponse(2, 120m));
				var feeRates = await mockRpc.Object.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative);
				var estimations = feeRates.Estimations;

				Assert.Subset(Constants.ConfirmationTargets.ToHashSet(), estimations.Keys.ToHashSet());
				Assert.Equal(estimations.Keys, estimations.Keys.OrderBy(x => x));
				Assert.Equal(estimations.Values, estimations.Values.OrderByDescending(x => x));
				Assert.All(estimations, (e) => Assert.True(e.Value >= mempoolInfo.MemPoolMinFee * 100_000));
			}
		}

		private static Mock<IRPCClient> CreateAndConfigureRpcClient(bool isSynchronized = true, bool hasPeersInfo = false, double memPoolMinFee = 0.00001000)
		{
			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync()).ReturnsAsync(
				new BlockchainInfo
				{
					Blocks = isSynchronized ? 100_000UL : 89_765UL,
					Headers = 100_000L
				});
			mockRpc.Setup(rpc => rpc.GetPeersInfoAsync()).ReturnsAsync(
				hasPeersInfo
					? new[] { new PeerInfo() }
					: Array.Empty<PeerInfo>());
			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync()).ReturnsAsync(
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
}
