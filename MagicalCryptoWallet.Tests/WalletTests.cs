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

			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");
			var dataFolder = Path.Combine(SharedFixture.DataDir, nameof(TestServicesAsync));
			Directory.CreateDirectory(SharedFixture.DataDir);

			var addressManagerFilePath = Path.Combine(SharedFixture.DataDir, $"AddressManager{network}.dat");
			var connectionParameters = new NodeConnectionParameters();
			AddressManager addressManager = null;
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
