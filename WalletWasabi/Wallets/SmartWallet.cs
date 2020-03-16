using Microsoft.Extensions.Hosting;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets
{
	public class SmartWallet : BackgroundService
	{
		public SmartWallet(string filePath)
		{
			KeyManager = KeyManager.FromFile(filePath);
		}

		public SmartWallet(KeyManager keyManager)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		public KeyManager KeyManager { get; }

		/// <summary>
		/// If the wallet is fully initialized and stopping wasn't requested.
		/// </summary>
		public bool IsAlive { get; private set; } = false;

		public WalletService Wallet { get; private set; }

		public void InitializeWalletService(BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, NodesGroup nodes, string dataDir, ServiceConfiguration serviceConfiguration, IFeeProvider feeProvider, CoreNode bitcoinCoreNode)
		{
			Wallet = new WalletService(bitcoinStore, KeyManager, synchronizer, nodes, dataDir, serviceConfiguration, feeProvider, bitcoinCoreNode);
		}

		/// <inheritdoc />
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Wallet.StartAsync(stoppingToken).ConfigureAwait(false);
			Wallet.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
			Wallet.ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;
			IsAlive = true;
		}

		private void ChaumianClient_OnDequeue(object sender, DequeueResult e)
		{
			var handler = OnDequeue;
			handler?.Invoke(sender, e);
		}

		private void TransactionProcessor_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			var handler = WalletRelevantTransactionProcessed;
			handler?.Invoke(sender, e);
		}

		/// <inheritdoc />
		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			var wasAlive = IsAlive;
			IsAlive = false;

			if (wasAlive)
			{
				Wallet.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				Wallet.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;
			}
			await base.StopAsync(cancellationToken).ConfigureAwait(false);

			var wallet = Wallet;
			if (wallet is { })
			{
				await wallet.StopAsync(cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
