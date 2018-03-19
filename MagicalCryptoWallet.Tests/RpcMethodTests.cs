using MagicalCryptoWallet.Tests.NodeBuilding;
using NBitcoin.RPC;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
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
				Assert.Equal(generatedBlock[0].GetHash(), latestBlock.Hash);
			}
		}

		[Fact]
		public async Task CanWaitForBlockFromRpcAsync()
		{
			using (var builder = await NodeBuilder.CreateAsync())
			{
				var rpc = (await builder.CreateNodeAsync()).CreateRpcClient();
				await builder.StartAllAsync();
				var generatedBlocks = builder.Nodes.First().Generate(10, true, false);
				var latestBlockHash = generatedBlocks.Last().GetHash();
				var latestBlockTask = rpc.WaitForBlockAsync(latestBlockHash);

				builder.Nodes.First().BroadcastBlocks(generatedBlocks);
				var latestBlock = await latestBlockTask;
				Assert.True(latestBlockTask.IsCompleted && !latestBlockTask.IsFaulted);
				Assert.Equal(latestBlockHash, latestBlock.Hash);
			}
		}
		#endregion
	}
}
