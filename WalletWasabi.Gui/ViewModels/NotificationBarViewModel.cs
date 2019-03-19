using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.Protocol;
using ReactiveUI;
using WalletWasabi.Services;
using Avalonia.Data.Converters;
using System.Globalization;
using WalletWasabi.Models;
using NBitcoin;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using WalletWasabi.Gui.Tabs;
using System.Reactive.Linq;
using WalletWasabi.Gui.Dialogs;
using System.Runtime.InteropServices;
using System.Reactive.Disposables;
using System.ComponentModel;

namespace WalletWasabi.Gui.ViewModels
{
	public enum NotificationTypeEnum
	{
		None,
		Info,
		Warning,
		Error
	}

	public class NotificationBarViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		public ReactiveCommand CloseCommand { get; }

		private string _notificationText;

		public string NotificationText
		{
			get => _notificationText;
			set => this.RaiseAndSetIfChanged(ref _notificationText, value);
		}

		private NotificationTypeEnum _notificationType;

		public NotificationTypeEnum NotificationType
		{
			get => _notificationType;
			set => this.RaiseAndSetIfChanged(ref _notificationType, value);
		}

		public NotificationBarViewModel()
		{
			NotificationText= "Address copied to the clipboard";
			NotificationType= NotificationTypeEnum.Info;
		}


		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
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
