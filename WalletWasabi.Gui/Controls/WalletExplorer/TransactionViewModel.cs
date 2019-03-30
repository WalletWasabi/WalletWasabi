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

		public CancellationTokenSource CancelClipboardNotification { get; set; }

		public async Task TryCopyTxIdToClipboardAsync()
		{
			try
			{
				CancelClipboardNotification?.Cancel();
				while (CancelClipboardNotification != null)
				{
					await Task.Delay(50);
				}
				CancelClipboardNotification = new CancellationTokenSource();

				var cancelToken = CancelClipboardNotification.Token;

				await Application.Current.Clipboard.SetTextAsync(TransactionId);
				cancelToken.ThrowIfCancellationRequested();

				ClipboardNotificationVisible = true;
				ClipboardNotificationOpacity = 1;

				await Task.Delay(1000, cancelToken);
				ClipboardNotificationOpacity = 0;
				await Task.Delay(1000, cancelToken);
				ClipboardNotificationVisible = false;
			}
			catch (Exception ex)
			{
				if (ex is OperationCanceledException || ex is TaskCanceledException)
				{
					return;
				}

				Logging.Logger.LogWarning<AddressViewModel>(ex);
			}
			finally
			{
				CancelClipboardNotification?.Dispose();
				CancelClipboardNotification = null;
			}
		}
	}
}
