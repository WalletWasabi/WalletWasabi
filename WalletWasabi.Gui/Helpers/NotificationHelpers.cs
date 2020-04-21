using Avalonia.Controls.Notifications;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Helpers
{
	public static class NotificationHelpers
	{
		private const int MaxTitleLength = 50;
		private const int DefaultNotificationTimeout = 7;
		private static INotificationManager NullNotificationManager { get; } = new NullNotificationManager();

		public static INotificationManager GetNotificationManager()
		{
			return Locator.Current.GetService<INotificationManager>() ?? NullNotificationManager;
		}

		public static void Notify(string message, string title, NotificationType type, Action onClick = null, object sender = null)
		{
			List<string> titles = new List<string>();

			if (!string.IsNullOrEmpty(title))
			{
				titles.Add(title);
			}

			string walletname = sender switch
			{
				Wallet wallet => wallet.WalletName,
				WalletViewModelBase walletViewModelBase => walletViewModelBase.WalletName,
				_ => ""
			};

			if (!string.IsNullOrEmpty(walletname))
			{
				titles.Add(walletname);
			}

			var fullTitle = string.Join(" - ", titles);

			fullTitle = fullTitle.Substring(0, Math.Min(fullTitle.Length, MaxTitleLength));

			RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(fullTitle, message, type, TimeSpan.FromSeconds(DefaultNotificationTimeout), onClick)));
		}

		public static void Success(string message, string title = "Success!", object sender = null)
		{
			Notify(message, title, NotificationType.Success, sender: sender);
		}

		public static void Information(string message, string title = "Info", object sender = null)
		{
			Notify(message, title, NotificationType.Information, sender: sender);
		}

		public static void Warning(string message, string title = "Warning!", object sender = null)
		{
			Notify(message, title, NotificationType.Warning, sender: sender);
		}

		public static void Error(string message, string title = "Error!", object sender = null)
		{
			Notify(message, title, NotificationType.Error, sender: sender);
		}
	}
}
