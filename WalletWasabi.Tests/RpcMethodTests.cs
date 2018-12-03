using WalletWasabi.Tests.NodeBuilding;
using NBitcoin.RPC;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Logging;

namespace WalletWasabi.Tests
{
	public class RpcMethodTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public RpcMethodTests(SharedFixture sharedFixture)
		{
			SharedFixture = sharedFixture;
		}

		#region RpcMethodTests

		[Fact]
		[Trait("Category", "RunOnCi")]
		public async Task CanWaitForNewBlockFromRpcAsync()
		{
			using (var builder = await NodeBuilder.CreateAsync())
			{
				var rpc = (await builder.CreateNodeAsync()).CreateRpcClient();
				await builder.StartAllAsync();
				var latestBlockTask = rpc.WaitForNewBlockAsync(2 * 1000); // wait for 2 seconds
				var generatedBlock = builder.Nodes.First().Generate(1);
				var latestBlock = await latestBlockTask;
				Assert.True(latestBlockTask.IsCompleted && !latestBlockTask.IsFaulted);
				Assert.Equal(generatedBlock[0].GetHash(), latestBlock.hash);
			}
		}

		[Fact]
		[Trait("Category", "RunOnCi")]
		public async Task CanWaitForBlockFromRpcAsync()
		{
			using (var builder = await NodeBuilder.CreateAsync())
			{
				var rpc = (await builder.CreateNodeAsync()).CreateRpcClient();
				await builder.StartAllAsync();
				var generatedBlocks = builder.Nodes.First().Generate(10);
				var latestBlockHash = generatedBlocks.Last().GetHash();
				var latestBlockTask = rpc.WaitForBlockAsync(latestBlockHash);

				builder.Nodes.First().BroadcastBlocks(generatedBlocks);
				var latestBlock = await latestBlockTask;
				Assert.True(latestBlockTask.IsCompleted && !latestBlockTask.IsFaulted);
				Assert.Equal(latestBlockHash, latestBlock.hash);
			}
		}

		[Fact]
		[Trait("Category", "RunOnCi")]
		public async Task AllFeeEstimateRpcAsync()
		{
			using (var builder = await NodeBuilder.CreateAsync())
			{
				var rpc = (await builder.CreateNodeAsync()).CreateRpcClient();
				await builder.StartAllAsync();

				var estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: true);
				Assert.Equal(144, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Conservative, estimations.Type);

				estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: true);
				Assert.Equal(145, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);

				await Assert.ThrowsAsync<NoEstimationException>(async () => await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: false, tolerateBitcoinCoreBrainfuck: true));

				estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: false);
				Assert.Equal(145, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);
			}
		}

		#endregion RpcMethodTests
	}
}
