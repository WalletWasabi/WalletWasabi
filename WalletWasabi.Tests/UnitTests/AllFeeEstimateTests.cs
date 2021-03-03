using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using Xunit;
using System;
using WalletWasabi.BitcoinCore.Rpc;
using Moq;

namespace WalletWasabi.Tests.UnitTests
{
	public class AllFeeEstimateTests
	{
		[Fact]
		public void AllFeeEstimateSerialization()
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

			Assert.Equal(estimations[2], deserialized.Estimations[2]);
			Assert.Equal(estimations[3], deserialized.Estimations[3]);
			Assert.Equal(estimations[19], deserialized.Estimations[36]);
			Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized.Type);
		}

		[Fact]
		public void AllFeeEstimateOrdersByTarget()
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
		public void AllFeeEstimateHandlesDuplicate()
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
		public void AllFeeEstimateHandlesInconsistentData()
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

			await Assert.ThrowsAsync<NoEstimationException>(async () => await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
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

			var ex = await Assert.ThrowsAsync<RPCException>(async () => await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
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
			
			var allFee = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.False(allFee.Estimations.ContainsKey(3));
			Assert.False(allFee.Estimations.ContainsKey(5));
			Assert.False(allFee.Estimations.ContainsKey(8));
		}

		[Fact]
		public async Task InaccurateEstimationsAsync()
		{
			var mockRpc = CreateAndConfigureRpcClient(isSynchronized: false, hasPeersInfo: true);
			var any = EstimateSmartFeeMode.Economical;
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(2, any)).ReturnsAsync(FeeRateResponse(2, 100m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(3, any)).ReturnsAsync(FeeRateResponse(3, 100m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(5, any)).ReturnsAsync(FeeRateResponse(5, 89m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(6, any)).ReturnsAsync(FeeRateResponse(2, 75m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(8, any)).ReturnsAsync(FeeRateResponse(2, 70m));
			mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsNotIn<int>(2, 3, 5, 6, 8), any)).ThrowsAsync(new NoEstimationException(0));

			var allFee = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical);
			Assert.False(allFee.IsAccurate);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.Equal(100, allFee.Estimations[2]);
			Assert.Equal(75, allFee.Estimations[6]);
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

			var allFee = await mockRpc.Object.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.True(allFee.IsAccurate);
			Assert.Equal(3, allFee.Estimations.Count);
			Assert.Equal(99, allFee.Estimations[2]);
			Assert.Equal(75, allFee.Estimations[6]);
			Assert.Equal(31, allFee.Estimations[1008]);
		}

		private static Mock<IRPCClient> CreateAndConfigureRpcClient(bool isSynchronized = true, bool hasPeersInfo = false, double memPoolMinFee = 0.00001000)
		{
			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetBlockchainInfoAsync()).ReturnsAsync(
				new BlockchainInfo
				{
					Blocks = isSynchronized ? 100_000L : 89_765L,
					Headers = 100_000L
				});
			mockRpc.Setup(rpc => rpc.GetPeersInfoAsync()).ReturnsAsync(
				hasPeersInfo 
					? new[] { new PeerInfo() } 
					: Array.Empty<PeerInfo>());
			mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync()).ReturnsAsync(
				new MemPoolInfo
				{
					MemPoolMinFee = memPoolMinFee // 1 s/b (default value)
				});
			mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);

			return mockRpc;
		}

		private static EstimateSmartFeeResponse FeeRateResponse(int target, decimal feeRate) =>
			new EstimateSmartFeeResponse
			{
				Blocks = target,
				FeeRate = new FeeRate(feeRate)
			};
	}
}
