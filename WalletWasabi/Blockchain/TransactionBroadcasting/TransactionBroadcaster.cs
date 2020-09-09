using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Blockchain.TransactionBroadcasting
{
	public class TransactionBroadcaster
	{
		public TransactionBroadcaster(Network network, BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, NodesGroup nodes, WalletManager walletManager, IRPCClient rpc)
		{
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			Network = Guard.NotNull(nameof(network), network);
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
			Synchronizer = Guard.NotNull(nameof(synchronizer), synchronizer);
			WalletManager = Guard.NotNull(nameof(walletManager), walletManager);
			RpcClient = rpc;
		}

		public BitcoinStore BitcoinStore { get; }
		public WasabiSynchronizer Synchronizer { get; }
		public Network Network { get; }
		public NodesGroup Nodes { get; }
		public IRPCClient RpcClient { get; private set; }
		public WalletManager WalletManager { get; }

		private async Task BroadcastTransactionToNetworkNodeAsync(SmartTransaction transaction, Node node)
		{
			Logger.LogInfo($"Trying to broadcast transaction with random node ({node.RemoteSocketAddress}):{transaction.GetHash()}.");
			if (!BitcoinStore.MempoolService.TryAddToBroadcastStore(transaction.Transaction, node.RemoteSocketEndpoint.ToString())) // So we'll reply to INV with this transaction.
			{
				Logger.LogWarning($"Transaction {transaction.GetHash()} was already present in the broadcast store.");
			}
			var invPayload = new InvPayload(transaction.Transaction);

			// Give 7 seconds to send the inv payload.
			await node.SendMessageAsync(invPayload).WithAwaitCancellationAsync(TimeSpan.FromSeconds(7)).ConfigureAwait(false); // ToDo: It's dangerous way to cancel. Implement proper cancellation to NBitcoin!

			if (BitcoinStore.MempoolService.TryGetFromBroadcastStore(transaction.GetHash(), out TransactionBroadcastEntry entry))
			{
				// Give 7 seconds for serving.
				var timeout = 0;
				while (!entry.IsBroadcasted())
				{
					if (timeout > 7)
					{
						throw new TimeoutException("Did not serve the transaction.");
					}
					await Task.Delay(1_000).ConfigureAwait(false);
					timeout++;
				}
				node.DisconnectAsync("Thank you!");
				Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}. Successfully broadcasted transaction: {transaction.GetHash()}.");

				// Give 21 seconds for propagation.
				timeout = 0;
				while (entry.GetPropagationConfirmations() < 2)
				{
					if (timeout > 21)
					{
						throw new TimeoutException("Did not serve the transaction.");
					}
					await Task.Delay(1_000).ConfigureAwait(false);
					timeout++;
				}
				Logger.LogInfo($"Transaction is successfully propagated: {transaction.GetHash()}.");
			}
			else
			{
				Logger.LogWarning($"Expected transaction {transaction.GetHash()} was not found in the broadcast store.");
			}
		}

		private async Task BroadcastTransactionToBackendAsync(SmartTransaction transaction)
		{
			Logger.LogInfo("Broadcasting with backend...");
			using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				try
				{
					await client.BroadcastAsync(transaction).ConfigureAwait(false);
				}
				catch (HttpRequestException ex2) when (RpcErrorTools.IsSpentError(ex2.Message))
				{
					if (transaction.Transaction.Inputs.Count == 1) // If we tried to only spend one coin, then we can mark it as spent. If there were more coins, then we do not know.
					{
						OutPoint input = transaction.Transaction.Inputs.First().PrevOut;
						foreach (var coin in WalletManager.CoinsByOutPoint(input))
						{
							coin.SpentAccordingToBackend = true;
						}
					}

					throw;
				}
			}

			BelieveTransaction(transaction);

			Logger.LogInfo($"Transaction is successfully broadcasted to backend: {transaction.GetHash()}.");
		}

		private void BelieveTransaction(SmartTransaction transaction)
		{
			if (transaction.Height == Height.Unknown)
			{
				transaction.SetUnconfirmed();
			}

			WalletManager.Process(transaction);
		}

		public async Task SendTransactionAsync(SmartTransaction transaction)
		{
			try
			{
				// Broadcast to a random node.
				// Wait until it arrives to at least two other nodes.
				// If something's wrong, fall back broadcasting with rpc, then backend.

				if (Network == Network.RegTest)
				{
					throw new InvalidOperationException($"Transaction broadcasting to nodes does not work in {Network.RegTest}.");
				}

				Node node = Nodes.ConnectedNodes.RandomElement();
				while (node is null || !node.IsConnected || Nodes.ConnectedNodes.Count < 5)
				{
					// As long as we are connected to at least 4 nodes, we can always try again.
					// 3 should be enough, but make it 5 so 2 nodes could disconnect in the meantime.
					if (Nodes.ConnectedNodes.Count < 5)
					{
						throw new InvalidOperationException("We are not connected to enough nodes.");
					}
					await Task.Delay(100).ConfigureAwait(false);
					node = Nodes.ConnectedNodes.RandomElement();
				}
				await BroadcastTransactionToNetworkNodeAsync(transaction, node).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Random node could not broadcast transaction. Reason: {ex.Message}.");
				Logger.LogDebug(ex);

				if (RpcClient != null)
				{
					try
					{
						await BroadcastTransactionWithRpcAsync(transaction).ConfigureAwait(false);
					}
					catch (Exception ex2)
					{
						Logger.LogInfo($"RPC could not broadcast transaction. Reason: {ex2.Message}.");
						Logger.LogDebug(ex2);

						await BroadcastTransactionToBackendAsync(transaction).ConfigureAwait(false);
					}
				}
				else
				{
					await BroadcastTransactionToBackendAsync(transaction).ConfigureAwait(false);
				}
			}
			finally
			{
				BitcoinStore.MempoolService.TryRemoveFromBroadcastStore(transaction.GetHash(), out _); // Remove it just to be sure. Probably has been removed previously.
			}
		}

		private async Task BroadcastTransactionWithRpcAsync(SmartTransaction transaction)
		{
			await RpcClient.SendRawTransactionAsync(transaction.Transaction).ConfigureAwait(false);
			BelieveTransaction(transaction);
			Logger.LogInfo($"Transaction is successfully broadcasted with RPC: {transaction.GetHash()}.");
		}
	}
}
