using MagicalCryptoWallet.Tests.NodeBuilding;
using NBitcoin.RPC;
using System.Linq;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
    public class RpcMethodTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public RpcMethodTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}
		
		#region RPCMethodTests

		[Fact]
		public void CanWaitForNewBlockFromRPC()
		{
			using(var builder = NodeBuilder.Create())
			{
				var rpc = builder.CreateNode().CreateRPCClient();
				builder.StartAll();
				var latestBlockTask = rpc.WaitForNewBlockAsync(2 * 1000); // wait for 2 seconds
				var generatedBlock = builder.Nodes.First().Generate(1);
				latestBlockTask.Wait();
				var latestBlock = latestBlockTask.Result;
				Assert.True(latestBlockTask.IsCompleted && !latestBlockTask.IsFaulted);
				Assert.Equal(generatedBlock[0].GetHash(), latestBlock.Hash);
			}
		}

		[Fact]
		public void CanWaitForBlockFromRPC()
		{
			using(var builder = NodeBuilder.Create())
			{
				var rpc = builder.CreateNode().CreateRPCClient();
				builder.StartAll();
				var generatedBlocks = builder.Nodes.First().Generate(10, true, false);
				var latestBlockHash = generatedBlocks.Last().GetHash();
				var latestBlockTask = rpc.WaitForBlockAsync(latestBlockHash);

				builder.Nodes.First().BroadcastBlocks(generatedBlocks);
				latestBlockTask.Wait();
				var latestBlock = latestBlockTask.Result;
				Assert.True(latestBlockTask.IsCompleted && !latestBlockTask.IsFaulted);
				Assert.Equal(latestBlockHash, latestBlock.Hash);
			}
		}
		#endregion
	}
}
