using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.CoinJoin.Client
{
	public class CoinJoinProcessor : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public CoinJoinProcessor(WasabiSynchronizer synchronizer, WalletManager walletManager, IRPCClient rpc)
		{
			Synchronizer = Guard.NotNull(nameof(synchronizer), synchronizer);
			WalletManager = Guard.NotNull(nameof(walletManager), walletManager);
			RpcClient = rpc;
			ProcessLock = new AsyncLock();
			Synchronizer.ResponseArrived += Synchronizer_ResponseArrivedAsync;
		}

		public WasabiSynchronizer Synchronizer { get; }
		public WalletManager WalletManager { get; }
		public IRPCClient RpcClient { get; private set; }
		private AsyncLock ProcessLock { get; }

		private async void Synchronizer_ResponseArrivedAsync(object sender, SynchronizeResponse response)
		{
			try
			{
				using (await ProcessLock.LockAsync())
				{
					var unconfirmedCoinJoinHashes = response.UnconfirmedCoinJoins;
					if (!unconfirmedCoinJoinHashes.Any())
					{
						return;
					}

					var txsNotKnownByAWallet = WalletManager.FilterUnknownCoinjoins(unconfirmedCoinJoinHashes);

					using var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint);

					var unconfirmedCoinJoins = await client.GetTransactionsAsync(Synchronizer.Network, txsNotKnownByAWallet, CancellationToken.None).ConfigureAwait(false);

					foreach (var tx in unconfirmedCoinJoins.Select(x => new SmartTransaction(x, Height.Mempool)))
					{
						if (RpcClient is null
							|| await TryBroadcastTransactionWithRpcAsync(tx).ConfigureAwait(false)
							|| (await RpcClient.TestAsync().ConfigureAwait(false)) is { }) // If the test throws exception then I believe it, because RPC is down and the backend is the god.
						{
							WalletManager.ProcessCoinJoin(tx);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private async Task<bool> TryBroadcastTransactionWithRpcAsync(SmartTransaction transaction)
		{
			try
			{
				await RpcClient.SendRawTransactionAsync(transaction.Transaction).ConfigureAwait(false);
				Logger.LogInfo($"Transaction is successfully broadcasted with RPC: {transaction.GetHash()}.");

				return true;
			}
			catch
			{
				return false;
			}
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Synchronizer.ResponseArrived -= Synchronizer_ResponseArrivedAsync;
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
