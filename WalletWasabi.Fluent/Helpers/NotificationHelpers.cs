using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class NotificationHelpers
{
	private const int DefaultNotificationTimeout = 10;
	private static WindowNotificationManager? NotificationManager;

	public static void SetNotificationManager(Visual host)
	{
		var notificationManager = new WindowNotificationManager(TopLevel.GetTopLevel(host))
		{
			Position = NotificationPosition.BottomRight,
			MaxItems = 4,
			Margin = new Thickness(0, 0, 15, 40)
		};

		NotificationManager = notificationManager;
	}

	public static void Show(string title, string message, Action? onClick = null)
	{
		if (NotificationManager is { } nm)
		{
			RxApp.MainThreadScheduler.Schedule(() => nm.Show(new Notification(title, message, NotificationType.Information, TimeSpan.FromSeconds(DefaultNotificationTimeout), onClick)));
		}
	}

	public static void Show(IWalletModel wallet, ProcessedResult result, Action onClick)
	{
		if (TryGetNotificationInputs(result, wallet.AmountProvider.UsdExchangeRate, out var message))
		{
			Show(wallet.Name, message, onClick);
		}
	}

	public static void Show(object viewModel)
	{
		NotificationManager?.Show(viewModel);
	}

	private static bool TryGetNotificationInputs(ProcessedResult result, decimal fiatExchangeRate, [NotNullWhen(true)] out string? message)
	{
		message = null;

		try
		{
			bool isSpent = result.NewlySpentCoins.Any();
			bool isReceived = result.NewlyReceivedCoins.Any();
			bool isConfirmedReceive = result.NewlyConfirmedReceivedCoins.Any();
			bool isConfirmedSpent = result.NewlyConfirmedReceivedCoins.Any();
			Money miningFee = result.Transaction.Transaction.GetFee(result.SpentCoins.Select(x => (ICoin)x.Coin).ToArray()) ?? Money.Zero;

			if (isReceived || isSpent)
			{
				Money receivedSum = result.NewlyReceivedCoins.Sum(x => x.Amount);
				Money spentSum = result.NewlySpentCoins.Sum(x => x.Amount);
				Money incoming = receivedSum - spentSum;
				Money receiveSpentDiff = incoming.Abs();
				string amountString = receiveSpentDiff.ToFormattedString();
				string fiatString = receiveSpentDiff.BtcToUsd(fiatExchangeRate).ToUsdAproxBetweenParens();

				if (result.Transaction.Transaction.IsCoinBase)
				{
					message = $"{amountString} BTC {fiatString} received as Coinbase reward";
				}
				else if (isSpent && receiveSpentDiff == miningFee)
				{
					message = $"Self transfer";
				}
				else if (incoming > Money.Zero)
				{
					message = $"{amountString} BTC {fiatString} incoming";
				}
				else if (incoming < Money.Zero)
				{
					var sentAmount = receiveSpentDiff - miningFee;
					var fiatSentAmount = sentAmount.BtcToUsd(fiatExchangeRate).ToUsdAproxBetweenParens();
					message = $"{sentAmount.ToFormattedString()} BTC {fiatSentAmount} sent";
				}
			}
			else if (isConfirmedReceive || isConfirmedSpent)
			{
				Money receivedSum = result.ReceivedCoins.Sum(x => x.Amount);
				Money spentSum = result.SpentCoins.Sum(x => x.Amount);
				Money incoming = receivedSum - spentSum;
				Money receiveSpentDiff = incoming.Abs();
				string amountString = receiveSpentDiff.ToFormattedString();
				string fiatString = receiveSpentDiff.BtcToUsd(fiatExchangeRate).ToUsdAproxBetweenParens();

				if (isConfirmedSpent && receiveSpentDiff == miningFee)
				{
					message = $"Self transfer confirmed";
				}
				else if (incoming > Money.Zero)
				{
					message = $"Receiving {amountString} BTC {fiatString} has been confirmed";
				}
				else if (incoming < Money.Zero)
				{
					var sentAmount = receiveSpentDiff - miningFee;
					var fiatSentAmount = sentAmount.BtcToUsd(fiatExchangeRate).ToUsdAproxBetweenParens();
					message = $"{sentAmount.ToFormattedString()} BTC {fiatSentAmount} sent got confirmed";
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}

		return message is { };
	}
}
