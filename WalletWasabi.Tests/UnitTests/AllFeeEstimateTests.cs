using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using Xunit;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;

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
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = async () =>
				await Task.FromResult(new BlockchainInfo
				{
					Blocks = 100,
					Headers = 100
				});
			rpc.OnGetPeersInfoAsync = async () =>
				await Task.FromResult(Array.Empty<PeerInfo>());
			rpc.OnGetMempoolInfoAsync = async () =>
				await Task.FromResult(new MemPoolInfo
				{
					MemPoolMinFee = 0.00001000 // 1 s/b (default value)
				});
			rpc.OnEstimateSmartFeeAsync = (target, _) =>
				throw new NoEstimationException(target);

			await Assert.ThrowsAsync<NoEstimationException>(async () => await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
		}

		[Fact]
		public async Task RpcFailuresAsync()
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBlockchainInfoAsync = () =>
				throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-GetBlockchainInfo", null);

			rpc.OnEstimateSmartFeeAsync = (target, _) =>
				throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-EstimateSmartFee", null);

			rpc.OnGetMempoolInfoAsync = async () =>
				await Task.FromResult(new MemPoolInfo
				{
					MemPoolMinFee = 0.00001000 // 1 s/b (default value)
				});

			var ex = await Assert.ThrowsAsync<RPCException>(async () => await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative));
			Assert.Equal(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, ex.RPCCode);
			Assert.Equal("Error-EstimateSmartFee", ex.Message);
		}

		[Fact]
		public async Task ToleratesRpcFailuresAsync()
		{
			var rpc = CreateAndConfigureRpcClient(
				estimator: target => target switch
				{
					2 => new FeeRate(100m),
					3 => throw new RPCException(RPCErrorCode.RPC_INTERNAL_ERROR, "Error", null),
					5 => new FeeRate(89m),
					6 => new FeeRate(75m),
					8 => new FeeRate(70m),
					_ => throw new NoEstimationException(target)
				}
			);

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.False(allFee.Estimations.ContainsKey(3));
			Assert.False(allFee.Estimations.ContainsKey(5));
			Assert.False(allFee.Estimations.ContainsKey(8));
		}

		[Fact]
		public async Task InaccurateEstimationsAsync()
		{
			var rpc = CreateAndConfigureRpcClient(
				estimator: target => target switch
				{
					2 => new FeeRate(100m),
					3 => new FeeRate(100m),
					5 => new FeeRate(89m),
					6 => new FeeRate(75m),
					8 => new FeeRate(70m),
					_ => throw new NoEstimationException(target)
				},
				isSynchronized: false,
				hasPeersInfo: true
			);

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical);
			Assert.False(allFee.IsAccurate);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.Equal(100, allFee.Estimations[2]);
			Assert.Equal(75, allFee.Estimations[6]);
		}

		[Fact]
		public async Task AccurateEstimationsAsync()
		{
			var rpc = CreateAndConfigureRpcClient(
				estimator: target => target switch
				{
					2 => new FeeRate(99m),
					3 => new FeeRate(99m),
					5 => new FeeRate(89m),
					6 => new FeeRate(75m),
					8 => new FeeRate(30m),
					11 => new FeeRate(30m),
					13 => new FeeRate(30m),
					15 => new FeeRate(30m),
					1008 => new FeeRate(31m),
					_ => throw new NoEstimationException(target)
				},
				hasPeersInfo: true
			);

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.True(allFee.IsAccurate);
			Assert.Equal(4, allFee.Estimations.Count);
			Assert.Equal(100, allFee.Estimations[2]);
			Assert.Equal(99, allFee.Estimations[3]);
			Assert.Equal(75, allFee.Estimations[6]);
			Assert.Equal(31, allFee.Estimations[1008]);
		}

		[Fact]
		public async Task FixObviouslyWrongEstimationsAsync()
		{
			var rpc = CreateAndConfigureRpcClient(
				estimator: target => target switch
				{
					2 => new FeeRate(120m),
					3 => new FeeRate(10m),
					6 => new FeeRate(3m),
					18 => new FeeRate(1m),
					36 => new FeeRate(1m),
					1008 => new FeeRate(1m),
					_ => throw new NoEstimationException(target)
				},
				hasPeersInfo: true
			);

			var allFee = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
			Assert.Equal(120, allFee.Estimations[2]);
			Assert.True(allFee.Estimations[3] > 10);
			Assert.True(allFee.Estimations[36] > 1);
		}

		[Fact]
		public async Task WorksWithBitcoinCoreEstimationsAsync()
		{
			var rpc = CreateAndConfigureRpcClient(
				estimator: target => target switch
				{
					2 => new FeeRate(120m),
					_ => throw new NoEstimationException(target)
				},
				hasPeersInfo: true
			);
			rpc.OnGetMempoolInfoAsync = async () => await Task.FromResult(
				new MemPoolInfo()
				{ 
					Histogram = Array.Empty<FeeRateGroup>()
				}
			);

			// Do not throw exception
			await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
		}

		[Fact]
		public async Task ExhaustiveEstimationsAsync()
		{
			foreach (var i in Enumerable.Range(0, 1000))
			{
				var mempoolInfo = MempoolInfoGenerator.GenerateMempoolInfo();
				var rpc = CreateAndConfigureRpcClient(
					estimator: MempoolInfoGenerator.GenerateFeeRateForTarget,
					hasPeersInfo: true
				);
				rpc.OnGetMempoolInfoAsync = async () => await Task.FromResult(mempoolInfo);
				var feeRates = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative);
				var estimations = feeRates.Estimations;

				Assert.Subset(Constants.ConfirmationTargets.ToHashSet(), estimations.Keys.ToHashSet());
				Assert.Equal(estimations.Keys, estimations.Keys.OrderBy(x => x));
				Assert.Equal(estimations.Values, estimations.Values.OrderByDescending(x => x));
				Assert.All(estimations, (e) => Assert.True(e.Value >= mempoolInfo.MemPoolMinFee * 100_000));
			}
		}

		private static MockRpcClient CreateAndConfigureRpcClient(Func<int, FeeRate> estimator, bool isSynchronized = true, bool hasPeersInfo = false, double mempoolMinFee = 0.00001000)
			=> new MockRpcClient()
			{
				OnGetBlockchainInfoAsync = async () =>
					await Task.FromResult(new BlockchainInfo
					{
						Blocks = isSynchronized ? 100_000L : 89_765L,
						Headers = 100_000L
					}),
				OnGetPeersInfoAsync = async () =>
					await Task.FromResult(hasPeersInfo ? new[] { new PeerInfo() } : Array.Empty<PeerInfo>()),
				OnGetMempoolInfoAsync = async () => await Task.FromResult(ParseMempoolInfo(mempoolMinFee)),
				OnEstimateSmartFeeAsync = async (target, _) =>
					await Task.FromResult(new EstimateSmartFeeResponse
					{
						Blocks = target,
						FeeRate = estimator(target)
					})
			};

		private static MemPoolInfo ParseMempoolInfo(double mempoolMinFee)
		{
			var mempoolInfoWithHistogram = File.ReadAllText("./UnitTests/Data/MempoolInfoWithHistogram.json");
			var jo = JObject.Parse(mempoolInfoWithHistogram);
			var feeHistogram = (JObject)jo["fee_histogram"];

			return new MemPoolInfo()
			{
				MemPoolMinFee = mempoolMinFee,
				Histogram = feeHistogram.Properties()
					.Where(p => p.Name != "total_fees")
					.Select( p => new FeeRateGroup
					{
						Group = int.Parse(p.Name),
						Sizes = p.Value.Value<ulong>("sizes"),
						Count = p.Value.Value<uint>("count"),
						Fees = Money.Satoshis(p.Value.Value<ulong>("fees")),
						From = new FeeRate(p.Value.Value<decimal>("from_feerate")),
						To = new FeeRate(Math.Min(100_000, p.Value.Value<decimal>("to_feerate")))
					})
					.ToArray()
			};
		}
	}
}