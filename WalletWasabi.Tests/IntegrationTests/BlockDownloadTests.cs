using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Daemon;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.BlockProvider;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;
using static WalletWasabi.Wallets.FilterProcessor.BlockDownloadService;

namespace WalletWasabi.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="BlockDownloadService"/>.
/// </summary>
public class BlockDownloadTests
{
	public static readonly SortedDictionary<uint, uint256> HeightToBlockHash = new()
	{
		{ 610_000, new uint256("0000000000000000000a6f607f74db48dae0a94022c10354536394c17672b7f7") },
		{ 610_001, new uint256("0000000000000000000b9cfe321443cb2ced7a4182b8594f077ee4e23d6c2ae2") },
		{ 610_002, new uint256("0000000000000000000aef4754dfc99fed93f40d8b90d06037075bc59af3ab4d") },
		{ 610_003, new uint256("00000000000000000012afa0710628d1c504f1d40e1fc1b71c8cc66982547d0b") },
		{ 610_004, new uint256("000000000000000000081bb924d567c05c39dabc7feda74b02a1327dc376634d") },
		{ 610_005, new uint256("0000000000000000000f68159a871ea320691afcb3e0b7e8e3e03f53c0c0d58d") },
		{ 610_006, new uint256("0000000000000000000571a7383c5679bf6967b6eb43efa807167ea3d1761e84") },
		{ 610_007, new uint256("0000000000000000000ab3657ecbfa7e4e555d58f4fe6a0279bee8ea29ae2df7") },
		{ 610_008, new uint256("000000000000000000157e58b222e0097afee38d6920f0750e177c0d02a5d593") },
		{ 610_009, new uint256("00000000000000000003354e9f48af4b5850fd884f38886d00b92bfd4a015d54") },
	};

	[Fact]
	public async Task BlockDownloadingTestAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(10));

		var fileSystemBlockRepository = new TesteableFileSystemBlockRepository
		{
			OnTryGetBlockAsync = (_, _) => Task.FromResult<Block?>(null),
			OnSaveAsync = (_, _) => Task.CompletedTask,
			OnRemoveAsync = (_,_) =>Task.CompletedTask,
		};

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

		P2PBlockProvider p2PBlockProvider = new(Network.Main, nodes);
		using BlockDownloadService blockDownloadService = new(fileSystemBlockRepository, trustedFullNodeBlockProviders: [], p2PBlockProvider);

		try
		{
			await blockDownloadService.StartAsync(testCts.Token);

			Stopwatch stopwatch = Stopwatch.StartNew();

			List<Task<Result<Block, DownloadError>>> tasks = [];

			foreach ((uint height, uint256 blockHash) in HeightToBlockHash)
			{
				var taskCompletionSource = blockDownloadService.TryGetBlockAsync(BlockSource.P2pNetwork, blockHash, height, testCts.Token);
				tasks.Add(taskCompletionSource);
			}

			await Task.WhenAll(tasks);

			uint blockHeight = HeightToBlockHash.First().Key;

			foreach (var task in tasks)
			{
				var result = await task;
				Logger.LogInfo($"Block #{blockHeight} finished with result: {result}");

				blockHeight++;
			}

			stopwatch.Stop();
			Logger.LogInfo($"Test finished in {stopwatch.ElapsedMilliseconds} ms");
		}
		finally
		{
			await blockDownloadService.StopAsync(testCts.Token);
		}
	}
}
