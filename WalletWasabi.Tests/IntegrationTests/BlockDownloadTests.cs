using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Daemon.Configuration;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class BlockDownloadTests
{
	private static readonly uint256[] HeightToBlockHash = [
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
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(10));

		string addressManagerFilePath = Path.Combine(Config.DataDir, "BitcoinP2pNetwork", $"AddressManager{Network.Main}.dat");
		AddressManager addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);
		AddressManagerBehavior addressManagerBehavior = new(addressManager)
		{
			Mode = AddressManagerBehaviorMode.Discover
		};

		using NodesGroup nodes = new(Network.Main);
		nodes.NodeConnectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
		nodes.Connect();

		while (nodes.ConnectedNodes.Count == 0)
		{
			await Task.Delay(1000, testCts.Token);
		}

		var p2PBlockProvider = BlockProviders.P2pBlockProvider(new P2PNodesManager(Network.Main, nodes));

		Stopwatch stopwatch = Stopwatch.StartNew();
		var tasks = new List<Task<Block?>>();

		foreach (uint256 blockHash in HeightToBlockHash)
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

		stopwatch.Stop();
		Logger.LogInfo($"Test finished in {stopwatch.ElapsedMilliseconds} ms");
	}
}
