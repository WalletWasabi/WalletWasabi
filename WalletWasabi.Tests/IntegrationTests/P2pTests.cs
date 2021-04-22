using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class P2pTests
	{
		[Theory]
		//[InlineData("test")]
		[InlineData("main")]
		public async Task TestServicesAsync(string networkString)
		{
			await RuntimeParams.LoadAsync();
			var network = Network.GetNetwork(networkString);
			var blocksToDownload = new List<uint256>();
			if (network == Network.Main)
			{
				blocksToDownload.Add(new uint256("00000000000000000037c2de35bd85f3e57f14ddd741ce6cee5b28e51473d5d0"));
				blocksToDownload.Add(new uint256("000000000000000000115315a43cb0cdfc4ea54a0e92bed127f4e395e718d8f9"));
				blocksToDownload.Add(new uint256("00000000000000000011b5b042ad0522b69aae36f7de796f563c895714bbd629"));
			}
			else if (network == Network.TestNet)
			{
				blocksToDownload.Add(new uint256("0000000097a664c4084b49faa6fd4417055cb8e5aac480abc31ddc57a8208524"));
				blocksToDownload.Add(new uint256("000000009ed5b82259ecd2aa4cd1f119db8da7a70e7ea78d9c9f603e01f93bcc"));
				blocksToDownload.Add(new uint256("00000000e6da8c2da304e9f5ad99c079df2c3803b49efded3061ecaf206ddc66"));
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}

			var dataDir = Common.GetWorkDir();

			await using var indexStore = new IndexStore(Path.Combine(dataDir, "indexStore"), network, new SmartHeaderChain());
			await using var transactionStore = new AllTransactionStore(Path.Combine(dataDir, "transactionStore"), network);
			var blocks = new FileSystemBlockRepository(Path.Combine(dataDir, "blocks"), network);
			await using BitcoinStore bitcoinStore = new(indexStore, transactionStore, blocks);
			await bitcoinStore.InitializeAsync();

			using var p2pNetwork = new P2pNetwork(network, new IPEndPoint(IPAddress.Loopback, 1234), null, Path.Combine(dataDir, "P2pNetwork"), bitcoinStore);

			HttpClientFactory httpClientFactory = new(Common.TorSocks5Endpoint, backendUriGetter: () => new Uri("http://localhost:12345"));
			ServiceConfiguration serviceConfig = new(MixUntilAnonymitySet.PrivacyLevelStrong.ToString(), 2, 21, 50, new IPEndPoint(IPAddress.Loopback, network.DefaultPort), Money.Coins(Constants.DefaultDustThreshold));
			CachedBlockProvider blockProvider = new(
				new P2pBlockProvider(p2pNetwork.Nodes, null, httpClientFactory, serviceConfig, network),
				bitcoinStore.BlockRepository);
			Assert.True(Directory.Exists(blocks.BlocksFolderPath));

			try
			{
				var mempoolTransactionAwaiter = new EventsAwaiter<SmartTransaction>(
					h => bitcoinStore.MempoolService.TransactionReceived += h,
					h => bitcoinStore.MempoolService.TransactionReceived -= h,
					3);

				var nodeConnectionAwaiter = new EventsAwaiter<NodeEventArgs>(
					h => p2pNetwork.Nodes.ConnectedNodes.Added += h,
					h => p2pNetwork.Nodes.ConnectedNodes.Added -= h,
					3);

				await p2pNetwork.StartAsync(CancellationToken.None);

				var downloadTasks = new List<Task<Block>>();
				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
				foreach (var hash in blocksToDownload)
				{
					downloadTasks.Add(blockProvider.GetBlockAsync(hash, cts.Token));
				}

				await nodeConnectionAwaiter.WaitAsync(TimeSpan.FromMinutes(3));

				var i = 0;
				var hashArray = blocksToDownload.ToArray();
				foreach (var block in await Task.WhenAll(downloadTasks))
				{
					Assert.True(File.Exists(Path.Combine(blocks.BlocksFolderPath, hashArray[i].ToString())));
					i++;
				}

				await mempoolTransactionAwaiter.WaitAsync(TimeSpan.FromMinutes(1));
			}
			finally
			{
				// So next test will download the block.
				foreach (var hash in blocksToDownload)
				{
					await blockProvider.BlockRepository.RemoveAsync(hash, CancellationToken.None);
				}

				if (Directory.Exists(blocks.BlocksFolderPath))
				{
					Directory.Delete(blocks.BlocksFolderPath, recursive: true);
				}

				await p2pNetwork.StopAsync(CancellationToken.None);

				await bitcoinStore.DisposeAsync();
			}
		}
	}
}
