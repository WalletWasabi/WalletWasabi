using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.Services;
using MagicalCryptoWallet.Tests.NodeBuilding;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
    public class P2pTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public P2pTests(SharedFixture fixture)
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

			var addressManagerFolderPath = Path.Combine(SharedFixture.DataDir, "AddressManager");
			var addressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{network}.dat");
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, $"Blocks{network}");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager addressManager = null;
			BlockDownloader downloader = null;
			try
			{
				try
				{
					addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);
					Logger.LogInfo<AddressManager>($"Loaded {nameof(AddressManager)} from `{addressManagerFilePath}`.");
				}
				catch(DirectoryNotFoundException ex)
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

				using (var nodes = new NodesGroup(network, connectionParameters,
					new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = ProtocolVersion.WITNESS_VERSION
					}))
				{
					downloader = new BlockDownloader(nodes, blocksFolderPath);
					Assert.True(Directory.Exists(blocksFolderPath));
					downloader.Start();
					foreach(var hash in blocksToDownload)
					{
						downloader.QueToDownload(hash);
					}

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

						foreach(var hash in blocksToDownload)
						{
							times = 0;
							while (downloader.GetBlock(hash) == null)
							{
								if (times > 1800) // 3 minutes
								{
									throw new TimeoutException($"{nameof(BlockDownloader)} test timed out.");
								}
								await Task.Delay(100);
								times++;
							}
							Assert.True(File.Exists(Path.Combine(blocksFolderPath, hash.ToString())));
							Logger.LogInfo<P2pTests>($"Full block is downloaded: {hash}.");
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
				downloader?.Stop();

				// So next test will download the block.
				foreach (var hash in blocksToDownload) 
				{
					downloader?.TryRemove(hash);
				}
				if (Directory.Exists(blocksFolderPath))
				{
					Directory.Delete(blocksFolderPath, recursive: true);
				}

				Directory.CreateDirectory(Path.GetDirectoryName(addressManagerFilePath));
				addressManager?.SavePeerFile(addressManagerFilePath, network);
				Logger.LogInfo<P2pTests>($"Saved {nameof(AddressManager)} to `{addressManagerFilePath}`.");
			}
		}

		private long _nodeCount = 0;
		private void ConnectedNodes_Added(object sender, NodeEventArgs e)
		{
			var nodes = sender as NodesCollection;
			Interlocked.Increment(ref _nodeCount);
			Logger.LogInfo<P2pTests>($"Node count:{Interlocked.Read(ref _nodeCount)}");
		}
		private void ConnectedNodes_Removed(object sender, NodeEventArgs e)
		{
			var nodes = sender as NodesCollection;
			Interlocked.Decrement(ref _nodeCount);
			// Trace is fine here, building the connections is more exciting than removing them.
			Logger.LogTrace<P2pTests>($"Node count:{Interlocked.Read(ref _nodeCount)}"); 
		}

		private long _mempoolTransactionCount = 0;
		private void MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
			Interlocked.Increment(ref _mempoolTransactionCount);
			Logger.LogInfo<P2pTests>($"Mempool transaction received: {e.GetHash()}.");
		}

		[Fact]
		public async Task FilterBuilderTestAsync()
		{
			using (var builder = NodeBuilder.Create())
			{
				builder.CreateNode();
				builder.StartAll();
				CoreNode regtestNode = builder.Nodes[0];
				regtestNode.Generate(101);
				RPCClient rpc = regtestNode.CreateRPCClient();

				var indexBuilderServiceDir = Path.Combine(SharedFixture.DataDir, nameof(IndexBuilderService));
				var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{rpc.Network}.dat");
				var utxoSetFilePath = Path.Combine(indexBuilderServiceDir, $"UtxoSet{rpc.Network}.dat");

				var indexBuilderService = new IndexBuilderService(rpc, indexFilePath, utxoSetFilePath);
				try
				{
					indexBuilderService.Syncronize();

					// Test initial syncronization.
					var times = 0;
					uint256 firstHash = await rpc.GetBlockHashAsync(0);
					while (indexBuilderService.GetFilters(firstHash).Count() != 101)
					{
						if (times > 500) // 30 sec
						{
							throw new TimeoutException($"{nameof(IndexBuilderService)} test timed out.");
						}
						await Task.Delay(100);
						times++;
					}

					// Test later syncronization.
					regtestNode.Generate(10);
					times = 0;
					while (indexBuilderService.GetFilters(firstHash).Count() != 111)
					{
						if (times > 500) // 30 sec
						{
							throw new TimeoutException($"{nameof(IndexBuilderService)} test timed out.");
						}
						await Task.Delay(100);
						times++;
					}

					// Test correct number of filters is received.
					var hundredthHash = await rpc.GetBlockHashAsync(100);
					Assert.Equal(11, indexBuilderService.GetFilters(hundredthHash).Count());
					var bestHash = await rpc.GetBestBlockHashAsync();
					Assert.Empty(indexBuilderService.GetFilters(bestHash));

					// Test filter block hashes are correct.
					var filters = indexBuilderService.GetFilters(firstHash).ToArray();
					for (int i = 0; i < 111; i++)
					{
						var expectedHash = await rpc.GetBlockHashAsync(i + 1);
						var filterModel = FilterModel.FromLine(filters[i], new Height(i));
						Assert.Equal(expectedHash, filterModel.BlockHash);
						Assert.Null(filterModel.Filter);
					}
				}
				finally
				{
					indexBuilderService.Stop();
				}
			}
		}
	}
}
