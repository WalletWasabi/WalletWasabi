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
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.CoinJoin.Client
{
	public class CoinJoinProcessor : IDisposable
	{
		public WasabiSynchronizer Synchronizer { get; }
		public Dictionary<WalletService, HashSet<uint256>> WalletServices { get; }
		public IEnumerable<KeyValuePair<WalletService, HashSet<uint256>>> AliveWalletServices => WalletServices.Where(x => x.Key is { IsDisposed: var isDisposed } && !isDisposed);
		public object WalletServicesLock { get; }
		public RPCClient RpcClient { get; private set; }
		private AsyncLock ProcessLock { get; }

		public CoinJoinProcessor(WasabiSynchronizer synchronizer, RPCClient rpc)
		{
			Synchronizer = Guard.NotNull(nameof(synchronizer), synchronizer);
			WalletServices = new Dictionary<WalletService, HashSet<uint256>>();
			WalletServicesLock = new object();
			RpcClient = rpc;
			ProcessLock = new AsyncLock();
			Synchronizer.ResponseArrived += Synchronizer_ResponseArrivedAsync;
		}

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

					var txsNotKnownByAWalletService = new HashSet<uint256>();
					lock (WalletServicesLock)
					{
						foreach (var pair in AliveWalletServices)
						{
							// If a wallet service doesn't know about the tx, then we add it for processing.
							foreach (var tx in unconfirmedCoinJoinHashes.Where(x => !pair.Value.Contains(x)))
							{
								txsNotKnownByAWalletService.Add(tx);
							}
						}
					}

					using var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint);

					var unconfirmedCoinJoins = await client.GetTransactionsAsync(Synchronizer.Network, txsNotKnownByAWalletService, CancellationToken.None).ConfigureAwait(false);

					foreach (Transaction tx in unconfirmedCoinJoins)
					{
						if (RpcClient is null
							|| await TryBroadcastTransactionWithRpcAsync(tx).ConfigureAwait(false)
							|| (await RpcClient.TestAsync().ConfigureAwait(false)) is { }) // If the test throws exception then I believe it, because RPC is down and the backend is the god.
						{
							BelieveTransaction(tx);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		public void AddWalletService(WalletService walletService)
		{
			Guard.NotNull(nameof(walletService), walletService);
			lock (WalletServicesLock)
			{
				WalletServices.Add(walletService, new HashSet<uint256>());
			}
		}

		private void BelieveTransaction(Transaction transaction)
		{
			lock (WalletServicesLock)
			{
				foreach (var pair in AliveWalletServices.Where(x => !x.Value.Contains(transaction.GetHash())))
				{
					var walletService = pair.Key;
					pair.Value.Add(transaction.GetHash());
					walletService.TransactionProcessor.Process(new SmartTransaction(transaction, Height.Mempool));
				}
			}
		}

		private async Task<bool> TryBroadcastTransactionWithRpcAsync(Transaction transaction)
		{
			try
			{
				await RpcClient.SendRawTransactionAsync(transaction).ConfigureAwait(false);
				Logger.LogInfo($"Transaction is successfully broadcasted with RPC: {transaction.GetHash()}.");

				return true;
			}
			catch
			{
				return false;
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

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
