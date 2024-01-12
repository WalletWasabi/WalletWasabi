using Moq;
using NBitcoin;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;
using System.Net;
using static WalletWasabi.Wallets.FilterProcessor.BlockDownloadService;
using System.IO;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;

namespace WalletWasabi.Tests.IntegrationTests;

public class BlockDownloadTests
{
	[Fact]
	public async Task BlockDownloadingTestAsync()
	{
		Mock<IFileSystemBlockRepository> mockFileSystemBlockRepository = new(MockBehavior.Strict);
		_ = mockFileSystemBlockRepository.Setup(c => c.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		RuntimeParams.SetDataDir(Path.Combine(Common.DataDir, "RegTests", "Backend"));
		await RuntimeParams.LoadAsync();

		var roamingDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

		var addressManagerFilePath = Path.Combine(roamingDir, "WalletWasabi", "Client", "BitcoinP2pNetwork", $"AddressManager{Network.Main}.dat");
		var addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);
		var addressManagerBehavior = new AddressManagerBehavior(addressManager)
		{
			Mode = AddressManagerBehaviorMode.Discover
		};

		using var nodes = new NodesGroup(Network.Main);
		nodes.NodeConnectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
		nodes.Connect();

		while (nodes.ConnectedNodes.Count == 0)
		{
			await Task.Delay(1000);
		}

		P2PBlockProvider p2PBlockProvider = new(Network.Main, nodes, false);
		using BlockDownloadService blockDownloadService = new(mockFileSystemBlockRepository.Object, p2PBlockProvider);

		await blockDownloadService.StartAsync(CancellationToken.None);
		var task = blockDownloadService.Enqueue(new uint256("00000000000000000001579944012cb54f9865da84a7422d51e1712c9d43f935"), new Priority(SyncType.Turbo)).Task;
		var result = await task;
		switch (result)
		{
			case SuccessResult successResult: break;
			default:
				break;
		}
	}
}
