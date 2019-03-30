using Avalonia;
using Avalonia.Threading;
using Gma.QrCodeNet.Encoding;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.ViewModels
{
	public class AddressViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		private bool _isExpanded;
		private bool[,] _qrCode;
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;
		private long _copyNotificationsInprocess = 0;

		public HdPubKey Model { get; }

		public AddressViewModel(HdPubKey model)
		{
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			// TODO fix this performance issue this should only be generated when accessed.
			Task.Run(() =>
			{
				var encoder = new QrEncoder(ErrorCorrectionLevel.M);
				encoder.TryEncode(Address, out var qrCode);

				return qrCode.Matrix.InternalArray;
			}).ContinueWith(x =>
			{
				QrCode = x.Result;
			});

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(AddressPrivate));
				this.RaisePropertyChanged(nameof(LabelPrivate));
			}).DisposeWith(Disposables);
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

		public string LabelPrivate => Global.UiConfig.LurkingWifeMode == true ? "###########" : Label;

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();

		public string AddressPrivate => Global.UiConfig.LurkingWifeMode == true ? "###########################" : Address;

		public string Pubkey => Model.PubKey.ToString();

		public string KeyPath => Model.FullKeyPath.ToString();

		public bool[,] QrCode
		{
			get => _qrCode;
			set => this.RaiseAndSetIfChanged(ref _qrCode, value);
		}

		public async Task TryCopyToClipboardAsync()
		{
			try
			{
				await Application.Current.Clipboard.SetTextAsync(Address);

				Interlocked.Increment(ref _copyNotificationsInprocess);
				ClipboardNotificationVisible = true;
				ClipboardNotificationOpacity = 1;

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
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<AddressViewModel>(ex);
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
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
