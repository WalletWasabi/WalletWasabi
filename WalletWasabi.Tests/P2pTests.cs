using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Crypto;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.NodeBuilding;
using WalletWasabi.Tests.XunitConfiguration;
using Xunit;

namespace WalletWasabi.Tests
{
	public class P2pTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public P2pTests(SharedFixture sharedFixture)
		{
			SharedFixture = sharedFixture;
		}

		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task TestServicesAsync(string networkString)
		{
			var network = Network.GetNetwork(networkString);
			var blocksToDownload = new HashSet<uint256>();
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
				throw new NotSupportedException(network.ToString());
			}

			var addressManagerFolderPath = Path.Combine(SharedFixture.DataDir, "AddressManager");
			var addressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{network}.dat");
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, "Blocks", network.ToString());
			var connectionParameters = new NodeConnectionParameters();
			AddressManager addressManager = null;
			try
			{
				addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);
				Logger.LogInfo<AddressManager>($"Loaded {nameof(AddressManager)} from `{addressManagerFilePath}`.");
			}
			catch (DirectoryNotFoundException ex)
			{
				Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{addressManagerFilePath}`. Initializing new one.");
				Logger.LogTrace<AddressManager>(ex);
				addressManager = new AddressManager();
			}
			catch (FileNotFoundException ex)
			{
				Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{addressManagerFilePath}`. Initializing new one.");
				Logger.LogTrace<AddressManager>(ex);
				addressManager = new AddressManager();
			}

			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
			var memPoolService = new MemPoolService();
			connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(memPoolService));

			var nodes = new NodesGroup(network, connectionParameters, requirements: Helpers.Constants.NodeRequirements);

			KeyManager keyManager = KeyManager.CreateNew(out _, "password");
			WasabiSynchronizer syncer = new WasabiSynchronizer(network, Path.Combine(SharedFixture.DataDir, nameof(TestServicesAsync), "IndexDownloader.txt"), new Uri("http://localhost:12345"));
			WalletService walletService = new WalletService(
			   keyManager,
			   syncer,
			   new CcjClient(syncer, network, keyManager, new Uri("http://localhost:12345")),
			   memPoolService,
			   nodes,
			   SharedFixture.DataDir);
			Assert.True(Directory.Exists(blocksFolderPath));

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
				while (Interlocked.Read(ref _mempoolTransactionCount) < 3)
				{
					if (times > 3000) // 3 minutes
					{
						throw new TimeoutException($"{nameof(MemPoolService)} test timed out.");
					}
					await Task.Delay(100);
					times++;
				}

				foreach (var hash in blocksToDownload)
				{
					using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
					{
						var block = await walletService.GetOrDownloadBlockAsync(hash, cts.Token);
						Assert.True(File.Exists(Path.Combine(blocksFolderPath, hash.ToString())));
						Logger.LogInfo<P2pTests>($"Full block is downloaded: {hash}.");
					}
				}
			}
			finally
			{
				nodes.ConnectedNodes.Added -= ConnectedNodes_Added;
				nodes.ConnectedNodes.Removed -= ConnectedNodes_Removed;
				memPoolService.TransactionReceived -= MemPoolService_TransactionReceived;

				// So next test will download the block.
				foreach (var hash in blocksToDownload)
				{
					await walletService?.DeleteBlockAsync(hash);
				}
				walletService?.Dispose();

				if (Directory.Exists(blocksFolderPath))
				{
					Directory.Delete(blocksFolderPath, recursive: true);
				}

				IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
				addressManager?.SavePeerFile(addressManagerFilePath, network);
				Logger.LogInfo<P2pTests>($"Saved {nameof(AddressManager)} to `{addressManagerFilePath}`.");
				nodes?.Dispose();
			}
		}

		private long _nodeCount = 0;

		private void ConnectedNodes_Added(object sender, NodeEventArgs e)
		{
			var nodes = sender as NodesCollection;
			Interlocked.Increment(ref _nodeCount);
			if (Interlocked.Read(ref _nodeCount) == 8)
			{
				Logger.LogTrace<P2pTests>($"Max node count reached: {Interlocked.Read(ref _nodeCount)}.");
			}

			Logger.LogTrace<P2pTests>($"Node count: {Interlocked.Read(ref _nodeCount)}.");
		}

		private void ConnectedNodes_Removed(object sender, NodeEventArgs e)
		{
			var nodes = sender as NodesCollection;
			Interlocked.Decrement(ref _nodeCount);
			// Trace is fine here, building the connections is more exciting than removing them.
			Logger.LogTrace<P2pTests>($"Node count: {Interlocked.Read(ref _nodeCount)}.");
		}

		private long _mempoolTransactionCount = 0;

		private void MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
			Interlocked.Increment(ref _mempoolTransactionCount);
			Logger.LogDebug<P2pTests>($"Mempool transaction received: {e.GetHash()}.");
		}
	}
}
