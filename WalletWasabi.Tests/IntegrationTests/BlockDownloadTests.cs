using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using WalletWasabi.Services;
using WalletWasabi.Services.NodesManagement;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class BlockDownloadTests
{
	private static readonly uint256[] BlockHashes = [
		 new uint256("0000000000000000000a6f607f74db48dae0a94022c10354536394c17672b7f7"),
		 new uint256("0000000000000000000b9cfe321443cb2ced7a4182b8594f077ee4e23d6c2ae2"),
		 new uint256("0000000000000000000aef4754dfc99fed93f40d8b90d06037075bc59af3ab4d"),
		 new uint256("00000000000000000012afa0710628d1c504f1d40e1fc1b71c8cc66982547d0b"),
		 new uint256("000000000000000000081bb924d567c05c39dabc7feda74b02a1327dc376634d"),
		 new uint256("0000000000000000000f68159a871ea320691afcb3e0b7e8e3e03f53c0c0d58d"),
		 new uint256("0000000000000000000571a7383c5679bf6967b6eb43efa807167ea3d1761e84"),
		 new uint256("0000000000000000000ab3657ecbfa7e4e555d58f4fe6a0279bee8ea29ae2df7"),
		 new uint256("000000000000000000157e58b222e0097afee38d6920f0750e177c0d02a5d593"),
		 new uint256("00000000000000000003354e9f48af4b5850fd884f38886d00b92bfd4a015d54"),
	];

	[Fact]
	public async Task BlockDownloadingTestAsync()
	{
		using var testCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

		var eventBus = new EventBus();
		using var p2pConnectionManager = new P2pConnectionManager(Network.Main, eventBus, DnsResolver.Instance, connectionTimeout: TimeSpan.FromSeconds(15));
		p2pConnectionManager.Start(testCts.Token);

		// Ticks are used by the nodes connection manager to re-evaluate connections.
		var tickTask = Task.Run(async () =>
		{
			while (!testCts.IsCancellationRequested)
			{
				eventBus.Publish(new Tick(DateTime.UtcNow));
				await Task.Delay(1000).ConfigureAwait(false);
			}
		});

		var p2PBlockProvider = BlockProviders.P2pBlockProvider(p2pConnectionManager.GetSingleUseNodeAsync);

		var tasks = new List<Task<Block?>>();

		foreach (uint256 blockHash in BlockHashes)
		{
			var taskCompletionSource = p2PBlockProvider(blockHash, testCts.Token);
			tasks.Add(taskCompletionSource);
		}

		await Task.WhenAll(tasks);

		foreach (var task in tasks)
		{
			var block = await task;
			Assert.NotNull(block);
		}

		// Stop the tick task.
		await testCts.CancelAsync();

		await tickTask;
	}

	[Fact]
	public async Task NodesAlreadyTriedAreAskedLastAsync()
	{
		using var testCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

		using var silentPeer1 = CreateSilentPeer(port: 28444);
		using var silentPeer2 = CreateSilentPeer(port: 28445);
		var seedNodes = (List<NetworkAddress>)Network.RegTest.SeedNodes;
		NetworkAddress[] silentPeers = [new(silentPeer1.LocalEndpoint), new(silentPeer2.LocalEndpoint)];
		seedNodes.AddRange(silentPeers);

		try
		{
			var eventBus = new EventBus();
			using var p2pConnectionManager = new P2pConnectionManager(Network.RegTest, eventBus, DnsResolver.Instance, connectionTimeout: TimeSpan.FromSeconds(15));
			p2pConnectionManager.Start(testCts.Token);

			while (p2pConnectionManager.Nodes.Length < 2)
			{
				eventBus.Publish(new Tick(DateTime.UtcNow));
				await Task.Delay(100, testCts.Token);
			}

			for (var i = 0; i < 20; i++)
			{
				var first = await p2pConnectionManager.GetSingleUseNodeAsync([], testCts.Token);
				var second = await p2pConnectionManager.GetSingleUseNodeAsync([first.Node], testCts.Token);
				Assert.NotSame(first.Node, second.Node);

				// Once every node was tried, any of them is acceptable again.
				var third = await p2pConnectionManager.GetSingleUseNodeAsync([first.Node, second.Node], testCts.Token);
				Assert.Contains(third.Node, new[] { first.Node, second.Node });
			}
		}
		finally
		{
			seedNodes.RemoveAll(x => silentPeers.Contains(x));
		}
	}

	/// <summary>A peer that completes the handshake but never answers anything else.</summary>
	private static NodeServer CreateSilentPeer(int port)
	{
		var peer = new NodeServer(Network.RegTest) { LocalEndpoint = new IPEndPoint(IPAddress.Loopback, port) };
		peer.InboundNodeConnectionParameters.TemplateBehaviors.Clear();
		peer.InboundNodeConnectionParameters.Services = NodeServices.Network;
		peer.Listen();
		return peer;
	}
}
