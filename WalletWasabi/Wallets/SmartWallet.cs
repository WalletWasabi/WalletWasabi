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

		public KeyManager KeyManager { get; }
		private WalletService Wallet { get; set; }

		public void InitializeWalletService(BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, NodesGroup nodes, string dataDir, ServiceConfiguration serviceConfiguration, IFeeProvider feeProvider, CoreNode bitcoinCoreNode)
		{
			Wallet = new WalletService(bitcoinStore, KeyManager, synchronizer, nodes, dataDir, serviceConfiguration, feeProvider, bitcoinCoreNode);
		}

		/// <inheritdoc />
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Wallet.StartAsync(stoppingToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken).ConfigureAwait(false);

			var wallet = Wallet;
			if (wallet is { })
			{
				await wallet.StopAsync(cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
