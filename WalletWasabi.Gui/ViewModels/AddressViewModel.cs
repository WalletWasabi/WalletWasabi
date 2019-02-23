using Avalonia;
using Avalonia.Threading;
using Gma.QrCodeNet.Encoding;
using ReactiveUI;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.ViewModels
{
	public class AddressViewModel : ViewModelBase
	{
		private bool _isExpanded;
		private bool[,] _qrCode;
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;

		public HdPubKey Model { get; }

		public AddressViewModel(HdPubKey model)
		{
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			Task.Run(() =>
			{
				var encoder = new QrEncoder(ErrorCorrectionLevel.M);
				encoder.TryEncode(Address, out var qrCode);

				return qrCode.Matrix.InternalArray;
			}).ContinueWith(x =>
			{
				QrCode = x.Result;
			});
		}

		public bool ClipboardNotificationVisible
		{
			get => _clipboardNotificationVisible;
			set => this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value);
		}

		public double ClipboardNotificationOpacity
		{
			get => _clipboardNotificationOpacity;
			set => this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value);
		}

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Label => Model.Label;

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();

		public string Pubkey => Model.PubKey.ToString();

		public string KeyPath => Model.FullKeyPath.ToString();

		public bool[,] QrCode
		{
			get => _qrCode;
			set => this.RaiseAndSetIfChanged(ref _qrCode, value);
		}

		private long _copyNotificationsInprocess = 0;

		public void CopyToClipboard()
		{
			Application.Current.Clipboard.SetTextAsync(Address).GetAwaiter().GetResult();

			Interlocked.Increment(ref _copyNotificationsInprocess);
			ClipboardNotificationVisible = true;
			ClipboardNotificationOpacity = 1;

			Dispatcher.UIThread.PostLogException(async () =>
			{
				try
				{
					await Task.Delay(1000);
					if (Interlocked.Read(ref _copyNotificationsInprocess) <= 1)
					{
						ClipboardNotificationOpacity = 0;
						await Task.Delay(1000);
						if (Interlocked.Read(ref _copyNotificationsInprocess) <= 1)
						{
							ClipboardNotificationVisible = false;
						}
					}
				}
				finally
				{
					Interlocked.Decrement(ref _copyNotificationsInprocess);
				}
			});
		}
	}
}
