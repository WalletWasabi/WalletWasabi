using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;
using static WalletWasabi.Wallets.FilterProcessor.BlockDownloadService;

namespace WalletWasabi.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="BlockDownloadService"/>.
/// </summary>
public class BlockDownloadTests
{
	[Fact]
	public async Task BlockDownloadingTestAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(10));

		Mock<IFileSystemBlockRepository> mockFileSystemBlockRepository = new(MockBehavior.Strict);
		_ = mockFileSystemBlockRepository.Setup(c => c.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		RuntimeParams.SetDataDir(Path.Combine(Common.DataDir, "RegTests", "Backend"));
		await RuntimeParams.LoadAsync();

		string roamingDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

		string addressManagerFilePath = Path.Combine(roamingDir, "WalletWasabi", "Client", "BitcoinP2pNetwork", $"AddressManager{Network.Main}.dat");
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

		P2PBlockProvider p2PBlockProvider = new(Network.Main, nodes, isTorEnabled: false);
		using BlockDownloadService blockDownloadService = new(mockFileSystemBlockRepository.Object, p2PBlockProvider);

		try
		{
			await blockDownloadService.StartAsync(testCts.Token);
			IResult result = await blockDownloadService.TryGetBlockAsync(new uint256("00000000000000000001579944012cb54f9865da84a7422d51e1712c9d43f935"), new Priority(SyncType.Turbo), maxAttempts: 1, testCts.Token);

			switch (result)
			{
				case SuccessResult successResult: break;
				default:
					break;
			}
		}
		finally
		{
			await blockDownloadService.StopAsync(testCts.Token);
		}
	}
}
