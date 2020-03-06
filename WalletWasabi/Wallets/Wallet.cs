using NBitcoin.Protocol;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets
{
	public class Wallet
	{
		private CancellationTokenSource _cancelWalletServiceInitialization = null;

		public WalletService WalletService { get; private set; }

		public Wallet()
		{

		}

		public Wallet(WalletService walletService)
		{
			WalletService = walletService;
		}

		public async Task InitializeWalletServiceAsync(
			WalletManager manager,
			BitcoinStore bitcoinStore,
			KeyManager keyManager,
			WasabiSynchronizer syncer,
			NodesGroup nodes,
			string workFolderDir,
			ServiceConfiguration serviceConfiguration,
			IFeeProvider feeProvider,
			CoreNode coreNode = null)
		{
			using (_cancelWalletServiceInitialization = new CancellationTokenSource())
			{
				var token = _cancelWalletServiceInitialization.Token;

				// TODO Check global initialize complete... should be done somewhere else before this is called.

				//while (!InitializationCompleted)
				//{
				//	await Task.Delay(100, token);
				//}

				WalletService = new WalletService(bitcoinStore, keyManager, syncer, nodes, workFolderDir, serviceConfiguration, feeProvider, coreNode);

				await manager.AddAndStartAsync(this, token).ConfigureAwait(false);
			}

			_cancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
		}

		public async Task DisposeInWalletDependentServicesAsync(WalletManager walletManager)
		{
			try
			{
				_cancelWalletServiceInitialization?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				Logger.LogWarning($"{nameof(_cancelWalletServiceInitialization)} is disposed. This can occur due to an error while processing the wallet.");
			}
			_cancelWalletServiceInitialization = null;

			await walletManager.RemoveAndStopAsync(this).ConfigureAwait(false);
		}
	}
}
