using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase
	{
		private TransactionInfo _model;
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;
		private long _copyNotificationsInprocess = 0;

		public TransactionViewModel(TransactionInfo model)
		{
			_model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;
		}

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(AmountBtc));
			this.RaisePropertyChanged(nameof(TransactionId));
			this.RaisePropertyChanged(nameof(DateTime));
		}

		public string DateTime => _model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		public bool Confirmed => _model.Confirmed;

		public string AmountBtc => _model.AmountBtc;

		public Money Amount => Money.TryParse(_model.AmountBtc, out Money money) ? money : Money.Zero;

		public string Label => _model.Label;

		public string TransactionId => _model.TransactionId;

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

		public async Task TryCopyTxIdToClipboardAsync()
		{
			try
			{
				await Application.Current.Clipboard.SetTextAsync(TransactionId);

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
	}
}
