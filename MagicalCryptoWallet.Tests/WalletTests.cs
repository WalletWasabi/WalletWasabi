using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.Services;
using NBitcoin;
using NBitcoin.Protocol;
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

		[Fact]
		public async Task TestServicesAsync()
		{
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");
			var dataFolder = Path.Combine(SharedFixture.DataDir, nameof(TestServicesAsync));
			using (var wallet = new WalletService(dataFolder, Network.Main, manager))
			{

				try
				{
					wallet.Nodes.ConnectedNodes.Added += ConnectedNodes_Added;
					wallet.Nodes.ConnectedNodes.Removed += ConnectedNodes_Removed;
					wallet.MemPoolService.TransactionReceived += MemPoolService_TransactionReceived;

					wallet.Start();
					// Using the interlocked, not because it makes sense in this context, but to
					// set an example that these values are often concurrency sensitive
					var times = 0;
					while (Interlocked.Read(ref _nodeCount) < 3)
					{
						if(times > 4200) // 7 minutes
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
					wallet.Nodes.ConnectedNodes.Added -= ConnectedNodes_Added;
					wallet.Nodes.ConnectedNodes.Removed -= ConnectedNodes_Removed;
					wallet.MemPoolService.TransactionReceived -= MemPoolService_TransactionReceived;
				}
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
