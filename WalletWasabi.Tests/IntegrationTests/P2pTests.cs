using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.BlockProviders;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class P2pTests
{
	[Theory]
	[InlineData("main")]
	public async Task TestServicesAsync(string networkString)
	{
		var network = Network.GetNetwork(networkString);
		var blocksToDownload = new List<uint256>();

		blocksToDownload.Add(new uint256("00000000000000000037c2de35bd85f3e57f14ddd741ce6cee5b28e51473d5d0"));
		blocksToDownload.Add(new uint256("000000000000000000115315a43cb0cdfc4ea54a0e92bed127f4e395e718d8f9"));
		blocksToDownload.Add(new uint256("00000000000000000011b5b042ad0522b69aae36f7de796f563c895714bbd629"));

		var eventBus = new EventBus();
		var dataDir = Common.GetWorkDir();

		SmartHeaderChain smartHeaderChain = new();
		await using var indexStore = new IndexStore(Path.Combine(dataDir, "indexStore"), network, smartHeaderChain);
		await using var transactionStore = new AllTransactionStore(Path.Combine(dataDir, "transactionStore"), network);
		var mempoolService = new MempoolService();
		var blocks = new FileSystemBlockRepository(Path.Combine(dataDir, "blocks"), network);
		BitcoinStore bitcoinStore = new(indexStore, transactionStore, mempoolService, smartHeaderChain, blocks);
		await bitcoinStore.InitializeAsync();

		var addressManagerFolderPath = Path.Combine(dataDir, "AddressManager");
		var addressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{network}.dat");
		var connectionParameters = new NodeConnectionParameters();
		AddressManager addressManager;

		try
		{
			addressManager = await NBitcoinHelpers.LoadAddressManagerFromPeerFileAsync(addressManagerFilePath);
			Logger.LogInfo($"Loaded {nameof(AddressManager)} from `{addressManagerFilePath}`.");
		}
		catch (DirectoryNotFoundException)
		{
			addressManager = new AddressManager();
		}
		catch (FileNotFoundException)
		{
			addressManager = new AddressManager();
		}
		catch (OverflowException)
		{
			File.Delete(addressManagerFilePath);
			addressManager = new AddressManager();
		}
		catch (FormatException)
		{
			File.Delete(addressManagerFilePath);
			addressManager = new AddressManager();
		}

		connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
		connectionParameters.TemplateBehaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		using var nodes = new NodesGroup(network, connectionParameters, requirements: Constants.NodeRequirements);

		KeyManager keyManager = KeyManager.CreateNew(out _, "password", network);
		var httpClientFactory = new CoordinatorHttpClientFactory(new Uri("http://localhost:12345"), new HttpClientFactory());
		var filterProvider = new WebApiFilterProvider(10_000, httpClientFactory, eventBus);
		using Synchronizer synchronizer = new(period: TimeSpan.FromSeconds(3), filterProvider, bitcoinStore, eventBus);
		using FeeRateEstimationUpdater feeProvider = new (TimeSpan.Zero, FeeRateProviders.BlockstreamAsync(new HttpClientFactory()), eventBus);

		using MemoryCache cache = new(new MemoryCacheOptions
		{
			SizeLimit = 1_000,
			ExpirationScanFrequency = TimeSpan.FromSeconds(30)
		});

		var blockProvider = new CachedBlockProvider(
			new P2PBlockProvider(network, nodes),
			blocks);

		ServiceConfiguration serviceConfiguration = new($"http://{IPAddress.Loopback}:{network.DefaultPort}", Money.Coins(Constants.DefaultDustThreshold));
		WalletFactory walletFactory = new(network, bitcoinStore, serviceConfiguration, feeProvider, blockProvider, eventBus);
		using Wallet wallet = walletFactory.CreateAndInitialize(keyManager);

		try
		{
			nodes.Connect();

			var downloadTasks = new List<Task<Block?>>();
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
			foreach (var hash in blocksToDownload)
			{
				downloadTasks.Add(blockProvider.TryGetBlockAsync(hash, cts.Token));
			}


			var i = 0;
			var hashArray = blocksToDownload.ToArray();
			foreach (var block in await Task.WhenAll(downloadTasks))
			{
				Assert.True(File.Exists(Path.Combine(blocks.BlocksFolderPath, hashArray[i].ToString())));
				i++;
			}
		}
		finally
		{
			if (wallet is { })
			{
				await wallet.StopAsync(CancellationToken.None);
			}

			if (Directory.Exists(blocks.BlocksFolderPath))
			{
				Directory.Delete(blocks.BlocksFolderPath, recursive: true);
			}

			IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
			addressManager?.SavePeerFile(addressManagerFilePath, network);
			Logger.LogInfo($"Saved {nameof(AddressManager)} to `{addressManagerFilePath}`.");
			cache.Dispose();
		}
	}
}
