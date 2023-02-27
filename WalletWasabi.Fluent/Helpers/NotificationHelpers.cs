using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class NotificationHelpers
{
	private const int DefaultNotificationTimeout = 10;
	private static WindowNotificationManager? NotificationManager;

	public static void SetNotificationManager(Window host)
	{
		var notificationManager = new WindowNotificationManager(host)
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

	public static void Show(Wallet wallet, ProcessedResult result, Action onClick)
	{
		if (TryGetNotificationInputs(wallet, result, out var message))
		{
			Show(wallet.WalletName, message, onClick);
		}
	}

	public static void Show(object viewModel)
	{
		NotificationManager?.Show(viewModel);
	}

	private static bool TryGetNotificationInputs(Wallet wallet, ProcessedResult result, [NotNullWhen(true)] out string? message)
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
				string fiatString = receiveSpentDiff.ToUsd(wallet.Synchronizer.UsdExchangeRate).ToUsdAmount();

				if (result.Transaction.Transaction.IsCoinBase)
				{
					message = $"{amountString} BTC ({fiatString} USD) received as Coinbase reward";
				}
				else if (isSpent && receiveSpentDiff == miningFee)
				{
					message = $"Self transfer";
				}
				else if (incoming > Money.Zero)
				{
					message = $"{amountString} BTC ({fiatString} USD) incoming";
				}
				else if (incoming < Money.Zero)
				{
					var sentAmount = receiveSpentDiff - miningFee;
					message = $"{sentAmount.ToFormattedString()} BTC ({fiatString} USD) sent";
				}
			}
			else if (isConfirmedReceive || isConfirmedSpent)
			{
				Money receivedSum = result.ReceivedCoins.Sum(x => x.Amount);
				Money spentSum = result.SpentCoins.Sum(x => x.Amount);
				Money incoming = receivedSum - spentSum;
				Money receiveSpentDiff = incoming.Abs();
				string amountString = receiveSpentDiff.ToFormattedString();
				string fiatString = receiveSpentDiff.ToUsd(wallet.Synchronizer.UsdExchangeRate).ToUsdAmount();

				if (isConfirmedSpent && receiveSpentDiff == miningFee)
				{
					message = $"Self transfer confirmed";
				}
				else if (incoming > Money.Zero)
				{
					message = $"Receiving {amountString} BTC ({fiatString} USD) has been confirmed";
				}
				else if (incoming < Money.Zero)
				{
					var sentAmount = receiveSpentDiff - miningFee;
					message = $"{sentAmount.ToFormattedString()} BTC ({fiatString} USD) sent got confirmed";
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
