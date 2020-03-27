using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.Crypto;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.P2p;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class P2pTests
	{
		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task TestServicesAsync(string networkString)
		{
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
			var dataDir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName());

			BitcoinStore bitcoinStore = new BitcoinStore(Path.Combine(dataDir, EnvironmentHelpers.GetMethodName()), network);
			await bitcoinStore.InitializeAsync();
			var blocksFolderPath = Path.Combine(dataDir, "Blocks", network.ToString());
			KeyManager keyManager = KeyManager.CreateNew(out _, "password");
			WasabiSynchronizer syncer = new WasabiSynchronizer(network, bitcoinStore, new Uri("http://localhost:12345"), Global.Instance.TorSocks5Endpoint);

			using var client = new P2pClient(dataDir, network, useTor: false, new IPEndPoint(IPAddress.Loopback, 1234), Global.Instance.TorSocks5Endpoint, bitcoinStore);
			using var startTimeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(7));
			await client.StartAsync(startTimeoutSource.Token);

			using Wallet wallet = Wallet.CreateAndRegisterServices(
				network,
				bitcoinStore,
				keyManager,
				syncer,
				client.Nodes,
				dataDir,
				new ServiceConfiguration(50, 2, 21, 50, new IPEndPoint(IPAddress.Loopback, network.DefaultPort), Money.Coins(Constants.DefaultDustThreshold)),
				syncer);
			Assert.True(Directory.Exists(blocksFolderPath));

			try
			{
				var mempoolTransactionAwaiter = new EventsAwaiter<SmartTransaction>(
					h => bitcoinStore.MempoolService.TransactionReceived += h,
					h => bitcoinStore.MempoolService.TransactionReceived -= h,
					3);

				var nodeConnectionAwaiter = new EventsAwaiter<NodeEventArgs>(
					h => client.Nodes.ConnectedNodes.Added += h,
					h => client.Nodes.ConnectedNodes.Added -= h,
					3 - client.Nodes.ConnectedNodes.Count);

				var downloadTasks = new List<Task<Block>>();
				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
				foreach (var hash in blocksToDownload)
				{
					downloadTasks.Add(wallet.FetchBlockAsync(hash, cts.Token));
				}

				await nodeConnectionAwaiter.WaitAsync(TimeSpan.FromMinutes(3));

				var i = 0;
				var hashArray = blocksToDownload.ToArray();
				foreach (var block in await Task.WhenAll(downloadTasks))
				{
					Assert.True(File.Exists(Path.Combine(blocksFolderPath, hashArray[i].ToString())));
					i++;
				}

				await mempoolTransactionAwaiter.WaitAsync(TimeSpan.FromMinutes(1));
			}
			finally
			{
				// So next test will download the block.
				foreach (var hash in blocksToDownload)
				{
					await wallet?.DeleteBlockAsync(hash);
				}
				if (wallet is { })
				{
					await wallet.StopAsync(CancellationToken.None);
				}

				if (Directory.Exists(blocksFolderPath))
				{
					Directory.Delete(blocksFolderPath, recursive: true);
				}

				await syncer.StopAsync();

				using var stopTimeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(7));
				await client.StopAsync(stopTimeoutSource.Token);
			}
		}
	}
}
