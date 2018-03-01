using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.Services;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
    public class WalletTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public WalletTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task TestServicesAsync(string networkString)
		{
			var network = Network.GetNetwork(networkString);
			var blocksToDownload = new HashSet<uint256>();
			if(network == Network.Main)
			{
				blocksToDownload.Add(new uint256("00000000000000000037c2de35bd85f3e57f14ddd741ce6cee5b28e51473d5d0"));
				blocksToDownload.Add(new uint256("000000000000000000115315a43cb0cdfc4ea54a0e92bed127f4e395e718d8f9"));
				blocksToDownload.Add(new uint256("00000000000000000011b5b042ad0522b69aae36f7de796f563c895714bbd629"));
			}
			else if(network == Network.TestNet)
			{
				blocksToDownload.Add(new uint256("0000000097a664c4084b49faa6fd4417055cb8e5aac480abc31ddc57a8208524"));
				blocksToDownload.Add(new uint256("000000009ed5b82259ecd2aa4cd1f119db8da7a70e7ea78d9c9f603e01f93bcc"));
				blocksToDownload.Add(new uint256("00000000e6da8c2da304e9f5ad99c079df2c3803b49efded3061ecaf206ddc66"));
			}
			else
			{
				throw new NotSupportedException(network.ToString());
			}

			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");
			var dataFolder = Path.Combine(SharedFixture.DataDir, nameof(TestServicesAsync));
			Directory.CreateDirectory(SharedFixture.DataDir);

			var addressManagerFilePath = Path.Combine(SharedFixture.DataDir, $"AddressManager{network}.dat");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager addressManager = null;
			BlockDownloader downloader = null;
			try
			{
				try
				{
					addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);
					Logger.LogInfo<WalletService>($"Loaded {nameof(AddressManager)} from `{addressManagerFilePath}`.");
				}
				catch (FileNotFoundException ex)
				{
					Logger.LogInfo<WalletService>($"{nameof(AddressManager)} did not exist at `{addressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace<WalletService>(ex);
					addressManager = new AddressManager();
				}

				connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
				var memPoolService = new MemPoolService();
				connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(memPoolService));

				using (var nodes = new NodesGroup(network, connectionParameters,
					new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = ProtocolVersion.WITNESS_VERSION
					}))
				{
					downloader = new BlockDownloader(nodes);
					downloader.Start();
					foreach(var hash in blocksToDownload)
					{
						downloader.TryQueToDownload(hash);
					}
					var wallet = new WalletService(dataFolder, network, manager, nodes, memPoolService);
					try
					{
						nodes.ConnectedNodes.Added += ConnectedNodes_Added;
						nodes.ConnectedNodes.Removed += ConnectedNodes_Removed;
						memPoolService.TransactionReceived += MemPoolService_TransactionReceived;

						nodes.Connect();
						// Using the interlocked, not because it makes sense in this context, but to
						// set an example that these values are often concurrency sensitive
						var times = 0;
						while (Interlocked.Read(ref _nodeCount) < 3)
						{
							if (times > 4200) // 7 minutes
							{
								throw new TimeoutException($"Connection test timed out.");
							}
							await Task.Delay(100);
							times++;
						}

						times = 0;
						while (Interlocked.Read(ref _mempoolTransactionCount) < 7)
						{
							if (times > 1800) // 3 minutes
							{
								throw new TimeoutException($"{nameof(MemPoolService)} test timed out.");
							}
							await Task.Delay(100);
							times++;
						}

						foreach(var hash in blocksToDownload)
						{
							times = 0;
							while (downloader.TryGetBlock(hash) == null)
							{
								if (times > 1800) // 3 minutes
								{
									throw new TimeoutException($"{nameof(MemPoolService)} test timed out.");
								}
								await Task.Delay(100);
								times++;
							}
							Logger.LogInfo<WalletTests>($"Full block is downloaded: {hash}.");
						}
					}
					finally
					{
						nodes.ConnectedNodes.Added -= ConnectedNodes_Added;
						nodes.ConnectedNodes.Removed -= ConnectedNodes_Removed;
						memPoolService.TransactionReceived -= MemPoolService_TransactionReceived;
					}
				}
			}
			finally
			{
				if (downloader != null)
				{
					await downloader.StopAsync();
				}
				addressManager?.SavePeerFile(addressManagerFilePath, network);
				Logger.LogInfo<WalletTests>($"Saved {nameof(AddressManager)} to `{addressManagerFilePath}`.");
			}
		}

		private long _nodeCount = 0;
		private void ConnectedNodes_Added(object sender, NodeEventArgs e)
		{
			var nodes = sender as NodesCollection;
			Interlocked.Increment(ref _nodeCount);
			Logger.LogInfo<WalletTests>($"Node count:{Interlocked.Read(ref _nodeCount)}");
		}
		private void ConnectedNodes_Removed(object sender, NodeEventArgs e)
		{
			var nodes = sender as NodesCollection;
			Interlocked.Decrement(ref _nodeCount);
			// Trace is fine here, building the connections is more exciting than removing them.
			Logger.LogTrace<WalletTests>($"Node count:{Interlocked.Read(ref _nodeCount)}"); 
		}

		private long _mempoolTransactionCount = 0;
		private void MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
			Interlocked.Increment(ref _mempoolTransactionCount);
			Logger.LogInfo<WalletTests>($"Mempool transaction received: {e.GetHash()}.");
		}
	}
}
