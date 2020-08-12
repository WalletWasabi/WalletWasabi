using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.JsonConverters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class AllFeeEstimateTests
	{
		[Fact]
		public void AllFeeEstimateSerialization()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 2, new FeeRate(102m) },
				{ 3, new FeeRate(20m) },
				{ 19, new FeeRate(1m) }
			};
			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			var serialized = JsonConvert.SerializeObject(allFee);
			var deserialized = JsonConvert.DeserializeObject<AllFeeEstimate>(serialized, new FeeRatePerKbJsonConverter());

			Assert.Equal(estimations[2], deserialized.Estimations[2]);
			Assert.Equal(estimations[3], deserialized.Estimations[3]);
			Assert.Equal(estimations[19], deserialized.Estimations[19]);
			Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized.Type);
		}

		[Fact]
		public void AllFeeEstimateOrdersByTarget()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 3, new FeeRate(20m) },
				{ 2, new FeeRate(102m) },
				{ 19, new FeeRate(1m) }
			};

			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
			Assert.Equal(estimations[3], allFee.Estimations[3]);
			Assert.Equal(estimations[19], allFee.Estimations[19]);
		}

		[Fact]
		public void AllFeeEstimateHandlesDuplicate()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 2, new FeeRate(20m) },
				{ 3, new FeeRate(20m) },
			};

			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(allFee.Estimations);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
		}

		[Fact]
		public void AllFeeEstimateHandlesInconsistentData()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 2, new FeeRate(20m) },
				{ 3, new FeeRate(21m) },
			};

			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(allFee.Estimations);
			Assert.Equal(estimations[2], allFee.Estimations[2]);

			estimations = new Dictionary<int, FeeRate>
			{
				{ 5, new FeeRate(1_000m) },
				{ 3, new FeeRate(21m) },
				{ 2, new FeeRate(20m) },
				{ 100, new FeeRate(100m) },
				{ 4, new FeeRate(4m) },
			};

			allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
			Assert.Equal(estimations[4], allFee.Estimations[4]);
		}


		[Fact]
		public async Task RpcNoEnoughEstimations()
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = async () =>
				await Task.FromResult(new BlockchainInfo
				{
					Blocks = 100,
					Headers = 100
				});
			rpc.OnGetPeersInfoAsync = async () =>
				await Task.FromResult(new PeerInfo[0]);

			rpc.OnEstimateSmartFeeAsync = (target, _) =>
				throw new NoEstimationException(target);

			await Assert.ThrowsAsync<NoEstimationException>(async () => await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
		}


		[Fact]
		public async Task RpcFailures()
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = () =>
				throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error", null);

			rpc.OnEstimateSmartFeeAsync = async (target, _) =>
				throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error", null);

			var ex = await Assert.ThrowsAsync<RPCException>(async () => await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
			Assert.Equal(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, ex.RPCCode);
			Assert.Equal("Error", ex.Message);
		}

		[Fact]
		public async Task ToleratesRpcFailures()
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = async () =>
				await Task.FromResult(new BlockchainInfo
				{
					Blocks = 100,
					Headers = 100
				});
			rpc.OnGetPeersInfoAsync = async () =>
				await Task.FromResult(new PeerInfo[0]);

			rpc.OnEstimateSmartFeeAsync = async (target, _) =>
				await Task.FromResult(new EstimateSmartFeeResponse
				{
					Blocks = target,
					FeeRate= target switch
					{
						2 => new FeeRate(100m),
						3 => throw new RPCException(RPCErrorCode.RPC_INTERNAL_ERROR, "Error", null),
						5 => new FeeRate(89m),
						6 => new FeeRate(75m),
						8 => new FeeRate(70m),
						_ => throw new NoEstimationException(target)
					}
				});

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.False(allFee.IsAccurate);
			Assert.Equal(3, allFee.Estimations.Count);
			Assert.Equal(100, allFee.Estimations[2].SatoshiPerByte);
			Assert.Equal(89, allFee.Estimations[5].SatoshiPerByte);
			Assert.Equal(70, allFee.Estimations[8].SatoshiPerByte);
		}


		[Fact]
		public async Task InaccurateEstimations()
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = async () =>
				await Task.FromResult(new BlockchainInfo
				{
					Blocks = 1,
					Headers = 2  // the node is not synchronized.
				});
			rpc.OnGetPeersInfoAsync = async () =>
				await Task.FromResult(new PeerInfo[0]);

			rpc.OnEstimateSmartFeeAsync = async (target, _) =>
				await Task.FromResult(new EstimateSmartFeeResponse
				{
					Blocks = target,
					FeeRate= target switch
					{
						2 => new FeeRate(100m),
						3 => new FeeRate(100m),
						5 => new FeeRate(89m),
						6 => new FeeRate(75m),
						8 => new FeeRate(70m),
						_ => throw new NoEstimationException(target)
					}
				});

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.False(allFee.IsAccurate);
			Assert.Equal(3, allFee.Estimations.Count);
			Assert.Equal(100, allFee.Estimations[2].SatoshiPerByte);
			Assert.Equal(89, allFee.Estimations[5].SatoshiPerByte);
			Assert.Equal(70, allFee.Estimations[8].SatoshiPerByte);
		}

		[Fact]
		public async Task AccurateEstimations()
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = async () =>
				await Task.FromResult(new BlockchainInfo
				{
					Blocks = 600_000,
					Headers = 600_000
				});
			rpc.OnGetPeersInfoAsync = async () =>
				await Task.FromResult(new PeerInfo[0]);

			rpc.OnEstimateSmartFeeAsync = async (target, _) =>
				await Task.FromResult(new EstimateSmartFeeResponse
				{
					Blocks = target,
					FeeRate= target switch
					{
						2 => new FeeRate(100m),
						3 => new FeeRate(100m),
						5 => new FeeRate(89m),
						6 => new FeeRate(75m),
						8 => new FeeRate(70m),
						11 => new FeeRate(30m),
						12 => new FeeRate(30m),
						15 => new FeeRate(30m),
						1008  => new FeeRate(35m),
						_ => throw new NoEstimationException(target)
					}
				});

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.False(allFee.IsAccurate);
			Assert.Equal(4, allFee.Estimations.Count);
			Assert.Equal(100, allFee.Estimations[2].SatoshiPerByte);
			Assert.Equal(89, allFee.Estimations[5].SatoshiPerByte);
			Assert.Equal(70, allFee.Estimations[8].SatoshiPerByte);
			Assert.Equal(35, allFee.Estimations[1008].SatoshiPerByte);
		}
	}
}
