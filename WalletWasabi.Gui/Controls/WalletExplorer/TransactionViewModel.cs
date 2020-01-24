using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase
	{
		private TransactionInfo Model { get; }
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;
		public ReactiveCommand<Unit, Unit> CopyTransactionId { get; }

		public TransactionViewModel(TransactionInfo model)
		{
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			CopyTransactionId = ReactiveCommand.CreateFromTask(TryCopyTxIdToClipboardAsync);

			CopyTransactionId.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});
		}

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(AmountBtc));
			this.RaisePropertyChanged(nameof(TransactionId));
			this.RaisePropertyChanged(nameof(DateTime));
		}

		public string DateTime => Model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		public bool Confirmed => Model.Confirmed;

		public int Confirmations => Model.Confirmations;

		public string AmountBtc => Model.AmountBtc;

		public Money Amount => Money.TryParse(Model.AmountBtc, out Money money) ? money : Money.Zero;

		public string Label => Model.Label;

		public string TransactionId => Model.TransactionId;

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
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				CancelClipboardNotification?.Dispose();
				CancelClipboardNotification = null;
			}
		}
	}
}
