using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class NodeBuildingTests
	{
		[Fact]
		public async Task CanBuildCoreNodeAsync()
		{
			CoreNode coreNode = await TestNodeBuilder.CreateAsync();
			await coreNode.TryStopAsync();
		}

		[Fact]
		public async Task NodesDifferAsync()
		{
			var coreNodes = await Task.WhenAll(TestNodeBuilder.CreateAsync(additionalFolder: "0"), TestNodeBuilder.CreateAsync(additionalFolder: "1"));
			CoreNode node1 = coreNodes[0];
			CoreNode node2 = coreNodes[1];
			try
			{
				Assert.NotEqual(node1.DataDir, node2.DataDir);
				Assert.NotEqual(node1.P2pEndPoint, node2.P2pEndPoint);
				Assert.NotEqual(node1.RpcEndPoint, node2.RpcEndPoint);
			}
			finally
			{
				await Task.WhenAll(node1.TryStopAsync(), node2.TryStopAsync());
			}
		}

		[Fact]
		public async Task RpcWorksAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			try
			{
				var blockCount = await coreNode.RpcClient.GetBlockCountAsync();
				Assert.Equal(0, blockCount);
			}
			finally
			{
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task P2pWorksAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			using var node = await coreNode.CreateP2pNodeAsync();
			try
			{
				var blocks = node.GetBlocks(new[] { Network.RegTest.GenesisHash });
				var genesis = Assert.Single(blocks);
				Assert.Equal(genesis.GetHash(), Network.RegTest.GenesisHash);
			}
			finally
			{
				node.Disconnect();
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task GetVersionTestsAsync()
		{
			using var cts = new CancellationTokenSource(7000);
			Version version = await CoreNode.GetVersionAsync(cts.Token);
			Assert.Equal(WalletWasabi.Helpers.Constants.BitcoinCoreVersion, version);
		}
	}
}
