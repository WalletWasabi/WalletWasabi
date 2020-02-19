using Avalonia.Controls.Notifications;
using NBitcoin;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Gui
{
	public class Wallet
	{
		private int _isDesperateDequeuing = 0;
		private CancellationTokenSource _cancelWalletServiceInitialization = null;

		private Wallet()
		{
		}

		private async Task InitaliseAsync (KeyManager keyManager)
		{
			WalletService = await CreateWalletServiceAsync(keyManager);

			AppDomain.CurrentDomain.ProcessExit += async (s, e) => await TryDesperateDequeueAllCoinsAsync();
		}

        public WalletService WalletService { get; private set; }

		public static async Task<Wallet> CreateWalletAsync (KeyManager keyManager)
		{
			var result = new Wallet();

			var global = Locator.Current.GetService<Global>();

			global.RegisterWallet(result);

			await result.InitaliseAsync(keyManager);

			return result;
		}

		private async Task<WalletService> CreateWalletServiceAsync(KeyManager keyManager)
		{
			WalletService walletService;

			var global = Locator.Current.GetService<Global>();

			using (_cancelWalletServiceInitialization = new CancellationTokenSource())
			{
				var token = _cancelWalletServiceInitialization.Token;
				while (!global.InitializationCompleted)
				{
					await Task.Delay(100, token);
				}

				walletService = new WalletService(global.BitcoinStore, keyManager, global.Synchronizer, global.Nodes, global.DataDir, global.Config.ServiceConfiguration, global.FeeProviders, global.BitcoinCoreNode);

				Logger.LogInfo($"Starting {nameof(WalletService)}...");
				await walletService.StartAsync(token);
				Logger.LogInfo($"{nameof(WalletService)} started.");

				token.ThrowIfCancellationRequested();

				global.TransactionBroadcaster.AddWalletService(walletService);
				global.CoinJoinProcessor.AddWalletService(walletService);

				walletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
				walletService.ChaumianClient.OnDequeue += ChaumianClient_OnDequeued;
			}
			_cancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.

			return walletService;
		}

		public async Task TryDesperateDequeueAllCoinsAsync()
		{
			// If already desperate dequeuing then return.
			// If not desperate dequeuing then make sure we're doing that.
			if (Interlocked.CompareExchange(ref _isDesperateDequeuing, 1, 0) == 1)
			{
				return;
			}
			try
			{
				await DesperateDequeueAllCoinsAsync();
			}
			catch (NotSupportedException ex)
			{
				Logger.LogWarning(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _isDesperateDequeuing, 0);
			}
		}

		public async Task DesperateDequeueAllCoinsAsync()
		{
			var enqueuedCoins = WalletService.Coins.CoinJoinInProcess().ToArray();
			if (enqueuedCoins.Any())
			{
				Logger.LogWarning("Unregistering coins in CoinJoin process.");
				await WalletService.ChaumianClient.DequeueCoinsFromMixAsync(enqueuedCoins, DequeueReason.ApplicationExit);
			}
		}

		public async Task DisposeInWalletDependentServicesAsync()
		{
			var global = Locator.Current.GetService<Global>();

			if (WalletService is { })
			{
				WalletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				WalletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeued;
			}

			try
			{
				_cancelWalletServiceInitialization?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				Logger.LogWarning($"{nameof(_cancelWalletServiceInitialization)} is disposed. This can occur due to an error while processing the wallet.");
			}
			_cancelWalletServiceInitialization = null;
			
			if (WalletService is { })
			{
				var keyManager = WalletService.KeyManager;
				if (keyManager is { }) // This should never happen.
				{
					string backupWalletFilePath = Path.Combine(global.WalletBackupsDir, Path.GetFileName(keyManager.FilePath));
					keyManager.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(WalletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
				}
				await WalletService.StopAsync(CancellationToken.None).ConfigureAwait(false);
				WalletService = null;
				Logger.LogInfo($"{nameof(WalletService)} is stopped.");
			}
		}

		private void ChaumianClient_OnDequeued(object sender, DequeueResult e)
		{
			try
			{
				var global = Locator.Current.GetService<Global>();

				if (global.UiConfig?.LurkingWifeMode is true)
				{
					return;
				}

				foreach (var success in e.Successful.Where(x => x.Value.Any()))
				{
					DequeueReason reason = success.Key;
					if (reason != DequeueReason.Spent)
					{
						var type = reason == DequeueReason.UserRequested ? NotificationType.Information : NotificationType.Warning;
						var message = reason == DequeueReason.UserRequested ? "" : reason.ToFriendlyString();
						var title = success.Value.Count() == 1 ? $"Coin ({success.Value.First().Amount.ToString(false, true)}) Dequeued" : $"{success.Value.Count()} Coins Dequeued";
						NotificationHelpers.Notify(message, title, type);
					}
				}

				foreach (var failure in e.Unsuccessful.Where(x => x.Value.Any()))
				{
					DequeueReason reason = failure.Key;
					var type = NotificationType.Warning;
					var message = reason.ToFriendlyString();
					var title = failure.Value.Count() == 1 ? $"Couldn't Dequeue Coin ({failure.Value.First().Amount.ToString(false, true)})" : $"Couldn't Dequeue {failure.Value.Count()} Coins";
					NotificationHelpers.Notify(message, title, type);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private void TransactionProcessor_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			try
			{
				var global = Locator.Current.GetService<Global>();

				// In lurking wife mode no notification is raised.
				// If there are no news, then don't bother too.
				if (global.UiConfig?.LurkingWifeMode is true || !e.IsNews)
				{
					return;
				}

				// ToDo
				// Double spent.
				// Anonymity set gained?
				// Received dust

				bool isSpent = e.NewlySpentCoins.Any();
				bool isReceived = e.NewlyReceivedCoins.Any();
				bool isConfirmedReceive = e.NewlyConfirmedReceivedCoins.Any();
				bool isConfirmedSpent = e.NewlyConfirmedReceivedCoins.Any();
				Money miningFee = e.Transaction.Transaction.GetFee(e.SpentCoins.Select(x => x.GetCoin()).ToArray());
				if (isReceived || isSpent)
				{
					Money receivedSum = e.NewlyReceivedCoins.Sum(x => x.Amount);
					Money spentSum = e.NewlySpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToString(false, true);

					if (e.Transaction.Transaction.IsCoinBase)
					{
						NotifyAndLog($"{amountString} BTC", "Mined", NotificationType.Success, e);
					}
					else if (isSpent && receiveSpentDiff == miningFee)
					{
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend", NotificationType.Information, e);
					}
					else if (isSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Completed!", "", NotificationType.Success, e);
					}
					else if (incoming > Money.Zero)
					{
						if (e.Transaction.IsRBF && e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacable Replacement Transaction", NotificationType.Information, e);
						}
						else if (e.Transaction.IsRBF)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacable Transaction", NotificationType.Success, e);
						}
						else if (e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacement Transaction", NotificationType.Information, e);
						}
						else
						{
							NotifyAndLog($"{amountString} BTC", "Received", NotificationType.Success, e);
						}
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Sent", NotificationType.Information, e);
					}
				}
				else if (isConfirmedReceive || isConfirmedSpent)
				{
					Money receivedSum = e.ReceivedCoins.Sum(x => x.Amount);
					Money spentSum = e.SpentCoins.Sum(x => x.Amount);
					Money incoming = receivedSum - spentSum;
					Money receiveSpentDiff = incoming.Abs();
					string amountString = receiveSpentDiff.ToString(false, true);

					if (isConfirmedSpent && receiveSpentDiff == miningFee)
					{
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend Confirmed", NotificationType.Information, e);
					}
					else if (isConfirmedSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Confirmed!", "", NotificationType.Information, e);
					}
					else if (incoming > Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Receive Confirmed", NotificationType.Information, e);
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Send Confirmed", NotificationType.Information, e);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private static void NotifyAndLog(string message, string title, NotificationType notificationType, ProcessedResult e)
		{
			message = Guard.Correct(message);
			title = Guard.Correct(title);
			NotificationHelpers.Notify(message, title, notificationType, async () => await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath));
			Logger.LogInfo($"Transaction Notification ({notificationType}): {title} - {message} - {e.Transaction.GetHash()}");
		}
	}
}
