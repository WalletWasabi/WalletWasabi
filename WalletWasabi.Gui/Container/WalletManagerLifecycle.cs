using System;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Wallets;
using WalletWasabi.Logging;
using Avalonia.Controls.Notifications;
using WalletWasabi.Helpers;
using WalletWasabi.Gui.Helpers;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Gui.Container
{
	public class WalletManagerLifecycle
	{
		public WalletManagerLifecycle(UiConfig uiConfig, WalletManager walletManager)
		{
			UiConfig = uiConfig;
			WalletManager = walletManager;
		}

		public UiConfig UiConfig { get; }
		public WalletManager WalletManager { get; }

		public void OnInit()
		{
			WalletManager.OnDequeue += WalletManager_OnDequeue;
			WalletManager.WalletRelevantTransactionProcessed += WalletManager_WalletRelevantTransactionProcessed;
		}

		public void OnDestroy()
		{
			WalletManager.OnDequeue -= WalletManager_OnDequeue;
			WalletManager.WalletRelevantTransactionProcessed -= WalletManager_WalletRelevantTransactionProcessed;
		}

		public void WalletManager_OnDequeue(object sender, DequeueResult e)
		{
			try
			{
				if (UiConfig.LurkingWifeMode)
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
						NotificationHelpers.Notify(message, title, type, sender: sender);
					}
				}

				foreach (var failure in e.Unsuccessful.Where(x => x.Value.Any()))
				{
					DequeueReason reason = failure.Key;
					var type = NotificationType.Warning;
					var message = reason.ToFriendlyString();
					var title = failure.Value.Count() == 1 ? $"Couldn't Dequeue Coin ({failure.Value.First().Amount.ToString(false, true)})" : $"Couldn't Dequeue {failure.Value.Count()} Coins";
					NotificationHelpers.Notify(message, title, type, sender: sender);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public void WalletManager_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			try
			{
				// In lurking wife mode no notification is raised.
				// If there are no news, then don't bother too.
				if (UiConfig.LurkingWifeMode || !e.IsNews || (sender as Wallet).State != WalletState.Started)
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
						NotifyAndLog($"{amountString} BTC", "Mined", NotificationType.Success, e, sender);
					}
					else if (isSpent && receiveSpentDiff == miningFee)
					{
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend", NotificationType.Information, e, sender);
					}
					else if (isSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Completed!", "", NotificationType.Success, e, sender);
					}
					else if (incoming > Money.Zero)
					{
						if (e.Transaction.IsRBF && e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replaceable Replacement Transaction", NotificationType.Information, e, sender);
						}
						else if (e.Transaction.IsRBF)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replaceable Transaction", NotificationType.Success, e, sender);
						}
						else if (e.Transaction.IsReplacement)
						{
							NotifyAndLog($"{amountString} BTC", "Received Replacement Transaction", NotificationType.Information, e, sender);
						}
						else
						{
							NotifyAndLog($"{amountString} BTC", "Received", NotificationType.Success, e, sender);
						}
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Sent", NotificationType.Information, e, sender);
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
						NotifyAndLog($"Mining Fee: {amountString} BTC", "Self Spend Confirmed", NotificationType.Information, e, sender);
					}
					else if (isConfirmedSpent && receiveSpentDiff.Almost(Money.Zero, Money.Coins(0.01m)) && e.IsLikelyOwnCoinJoin)
					{
						NotifyAndLog($"CoinJoin Confirmed!", "", NotificationType.Information, e, sender);
					}
					else if (incoming > Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Receive Confirmed", NotificationType.Information, e, sender);
					}
					else if (incoming < Money.Zero)
					{
						NotifyAndLog($"{amountString} BTC", "Send Confirmed", NotificationType.Information, e, sender);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private void NotifyAndLog(string message, string title, NotificationType notificationType, ProcessedResult e, object sender)
		{
			message = Guard.Correct(message);
			title = Guard.Correct(title);
			NotificationHelpers.Notify(message, title, notificationType, async () => await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath), sender);
			Logger.LogInfo($"Transaction Notification ({notificationType}): {title} - {message} - {e.Transaction.GetHash()}");
		}
	}
}
