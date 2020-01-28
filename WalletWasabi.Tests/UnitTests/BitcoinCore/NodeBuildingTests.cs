using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class NodeBuildingTests
	{
		[Fact]
		public async Task CanBuildCoreNodeAsync()
		{
			using var services = new HostedServices();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync(CancellationToken.None);
			await coreNode.TryStopAsync();
			await services.StopAllAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NodesDifferAsync()
		{
			using var services1 = new HostedServices();
			using var services2 = new HostedServices();
			var coreNodes = await Task.WhenAll(TestNodeBuilder.CreateAsync(services1, additionalFolder: "0"), TestNodeBuilder.CreateAsync(services2, additionalFolder: "1"));
			await services1.StartAllAsync(CancellationToken.None);
			await services2.StartAllAsync(CancellationToken.None);
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
				await services1.StopAllAsync(CancellationToken.None);
				await services2.StopAllAsync(CancellationToken.None);
				await Task.WhenAll(node1.TryStopAsync(), node2.TryStopAsync());
			}
		}

		[Fact]
		public async Task RpcWorksAsync()
		{
			using var services = new HostedServices();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync(CancellationToken.None);
			try
			{
				var blockCount = await coreNode.RpcClient.GetBlockCountAsync();
				Assert.Equal(0, blockCount);
			}
			finally
			{
				await services.StopAllAsync(CancellationToken.None);
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task P2pWorksAsync()
		{
			using var services = new HostedServices();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync(CancellationToken.None);
			using var node = await coreNode.CreateNewP2pNodeAsync();
			try
			{
				var blocks = node.GetBlocks(new[] { Network.RegTest.GenesisHash });
				var genesis = Assert.Single(blocks);
				Assert.Equal(genesis.GetHash(), Network.RegTest.GenesisHash);
			}
			finally
			{
				await services.StopAllAsync(CancellationToken.None);
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
