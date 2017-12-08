using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using NBitcoin;
using System.Diagnostics;
using System.Collections;

namespace HiddenWallet.FullSpv.MemPool
{
	public class NewTransactionEventArgs : EventArgs
	{
		public Transaction Transaction { get; }
		public int MempoolTxCount { get; }

		public NewTransactionEventArgs(Transaction transaction, int mempoolTxCount)
		{
			Transaction = transaction;
			MempoolTxCount = mempoolTxCount;
		}
	}
	public class MemPoolJob
	{
		private ConcurrentHashSet<uint256> _transactions = new ConcurrentHashSet<uint256>();
		public ConcurrentHashSet<uint256> Transactions { get => _transactions; private set => _transactions = value; }
		private ConcurrentHashSet<uint256> _notNeededTransactions = new ConcurrentHashSet<uint256>();

		public event EventHandler<NewTransactionEventArgs> NewTransaction;
		private void OnNewTransaction(Transaction transaction) => NewTransaction?.Invoke(this, new NewTransactionEventArgs(transaction, Transactions.Count));

		public bool SyncedOnce { get; private set; } = false;
		public event EventHandler Synced;
		private void OnSynced() => Synced?.Invoke(this, EventArgs.Empty);

		public bool ForcefullyStopped { get; set; } = false;
		internal bool Enabled { get; set; } = true;
		private bool ShouldNotRunYet => !Enabled || ForcefullyStopped || WalletJob.Nodes.ConnectedNodes.Count <= 3;
		public WalletJob WalletJob { get; }

		public MemPoolJob(WalletJob walletJob)
		{
			WalletJob = walletJob ?? throw new ArgumentNullException(nameof(walletJob));
		}

		public async Task StartAsync(CancellationToken ctsToken)
		{
			try
			{
				while (true)
				{
					try
					{
						if (ctsToken.IsCancellationRequested) return;
						if (ShouldNotRunYet)
						{
							_notNeededTransactions.Clear(); // should not grow infinitely
							await Task.Delay(100, ctsToken).ContinueWith(t => { });
							continue;
						}

						var currentMemPoolTransactions = await UpdateAsync(ctsToken);
						if (ctsToken.IsCancellationRequested) return;

						// Clear the transactions from the previous cycle
						Transactions = new ConcurrentHashSet<uint256>(currentMemPoolTransactions);
						_notNeededTransactions.Clear();

						if (!SyncedOnce)
						{
							SyncedOnce = true;
						}
						OnSynced();

						await Task.Delay(TimeSpan.FromMinutes(3), ctsToken).ContinueWith(t => { });
					}
					catch (OperationCanceledException)
					{
						continue;
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"Ignoring {nameof(MemPoolJob)} exception:");
						Debug.WriteLine(ex);
					}
				}
			}
			finally
			{
				_getMempoolTransactionsTimeoutSource?.Dispose();
			}
		}

		private int _getMempoolTransactionsTimeoutSec = 30;
		private CancellationTokenSource _getMempoolTransactionsTimeoutSource;
		private async Task<IEnumerable<uint256>> UpdateAsync(CancellationToken ctsToken)
		{
			var txidsWeAlreadyHadAndFound = new HashSet<uint256>();

			foreach (var node in WalletJob.Nodes.ConnectedNodes)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return txidsWeAlreadyHadAndFound;
					if (!node.IsConnected) continue;

					var txidsWeNeed = new HashSet<uint256>();
					var sw = new Stopwatch();
					sw.Start();
					using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
					{
						var ctsTokenGetMempool = CancellationTokenSource.CreateLinkedTokenSource(ctsToken, timeout.Token);
						var txidsOfNode = await Task.Run(() => node.GetMempool(ctsTokenGetMempool.Token));
						sw.Stop();
						Debug.WriteLine($"GetMempool(), txs: {txidsOfNode.Count()}, secs: {sw.Elapsed.TotalSeconds}");
						if (ShouldNotRunYet) break;

						foreach (var txid in txidsOfNode)
						{
							// if we had it in prevcycle note we found it again
							if (Transactions.Contains(txid)) txidsWeAlreadyHadAndFound.Add(txid);
							else if (_notNeededTransactions.Contains(txid))
							{
								// we don't need, do nothing
							}
							// if we didn't have it in prevcicle note we need it
							else txidsWeNeed.Add(txid);
						}
						var txIdsPieces = CollectionHelpers.Split(txidsWeNeed.ToArray(), 500);

						if (ctsToken.IsCancellationRequested) continue;
						if (!node.IsConnected) continue;

						foreach (var txIdsPiece in txIdsPieces)
						{
							sw.Restart();
							_getMempoolTransactionsTimeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_getMempoolTransactionsTimeoutSec));
							var ctsTokenGetMempoolTransactions = CancellationTokenSource.CreateLinkedTokenSource(ctsToken, _getMempoolTransactionsTimeoutSource.Token);
							Transaction[] txsPiece = await Task.Run(() => node.GetMempoolTransactions(txIdsPiece.ToArray(), ctsTokenGetMempoolTransactions.Token));
							sw.Stop();
							Debug.WriteLine($"GetMempoolTransactions(), asked txs: {txIdsPiece.Count()}, actual txs: {txsPiece.Count()}, secs: {sw.Elapsed.TotalSeconds}");

							// if node doesn't give us pieces disconnect				
							if (txIdsPiece.Count() != 0 && txsPiece.Count() == 0)
							{
								node.Disconnect();
								node.Dispose();
								Debug.WriteLine("Disconnected node, because it did not return transactions.");
								break;
							}

							foreach (
								var tx in
								txsPiece)
							{
								if (!node.IsConnected) continue;
								if (ctsToken.IsCancellationRequested) continue;

								// note we found it and add to unprocessed
								if (txidsWeAlreadyHadAndFound.Add(tx.GetHash()))
								{
									if (!_notNeededTransactions.Contains(tx.GetHash()))
									{
										if (Transactions.Add(tx.GetHash()))
										{
											OnNewTransaction(tx);
										}
									}
								}
							}
							if (ShouldNotRunYet) break;
						}

						// if the node has very few transactions disconnect it					
						if (node.IsConnected && WalletJob.CurrentNetwork == Network.Main && txidsOfNode.Count() <= 1)
						{
							node.Disconnect();
							node.Dispose();
							Debug.WriteLine("Disconnected node, because it has too few transactions.");
						}
					}
				}
				catch (OperationCanceledException ex)
				{
					if (_getMempoolTransactionsTimeoutSource.IsCancellationRequested)
					{
						_getMempoolTransactionsTimeoutSec++;
						Debug.WriteLine($"New GetMempoolTransactions() timeout: {_getMempoolTransactionsTimeoutSec}");
					}

					if (!ctsToken.IsCancellationRequested)
					{
						Debug.WriteLine($"Node exception in MemPool, disconnect node, continue with next node:");
						Debug.WriteLine(ex);
						try
						{
							node.Disconnect();
							node.Dispose();
						}
						catch { }
					}
					continue;
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Node exception in MemPool, disconnect node, continue with next node:");
					Debug.WriteLine(ex);
					try
					{
						node.Disconnect();
						node.Dispose();
					}
					catch { }
					continue;
				}

				Debug.WriteLine($"Mirrored a node's full MemPool. Local MemPool transaction count: {Transactions.Count}");
			}
			foreach (var notneeded in _notNeededTransactions)
			{
				txidsWeAlreadyHadAndFound.Remove(notneeded);
			}
			return txidsWeAlreadyHadAndFound;
		}

		public void RemoveTransactions(IEnumerable<uint256> transactionsToRemove)
		{
			foreach (var tx in transactionsToRemove)
			{
				_notNeededTransactions.Add(tx);
			}
			if (Transactions.Count() == 0) return;
			foreach (var tx in transactionsToRemove)
			{
				Transactions.TryRemove(tx);
			}
		}

		public bool TryAddNewTransaction(Transaction tx)
		{
			if (ForcefullyStopped) return false;
			if (!Enabled) return false;

			uint256 hash = tx.GetHash();
			if (!_notNeededTransactions.Contains(hash))
			{
				if (Transactions.Add(hash))
				{
					OnNewTransaction(tx);
					return true;
				}
			}
			return false;
		}
	}
}