using Avalonia;
using NBitcoin;
using ReactiveUI;
using System;
using System.Linq;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using WalletWasabi.Gui.Controls.TransactionDetails.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase
	{
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;

		public TransactionViewModel(TransactionDetailsViewModel model)
		{
			TransactionDetails = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			CopyTransactionId = ReactiveCommand.CreateFromTask(TryCopyTxIdToClipboardAsync);

			OpenTransactionInfo = ReactiveCommand.Create(() =>
			{
				var shell = IoC.Get<IShell>();

				var transactionInfo = shell.Documents?.OfType<TransactionInfoTabViewModel>()?.FirstOrDefault(x => x.Transaction?.TransactionId == TransactionId);

				if (transactionInfo is null)
				{
					transactionInfo = new TransactionInfoTabViewModel(TransactionDetails);
					shell.AddDocument(transactionInfo);
				}

				shell.Select(transactionInfo);
			});

			Observable
				.Merge(CopyTransactionId.ThrownExceptions)
				.Merge(OpenTransactionInfo.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});
		}

		private TransactionDetailsViewModel TransactionDetails { get; }

		public ReactiveCommand<Unit, Unit> CopyTransactionId { get; }

		public ReactiveCommand<Unit, Unit> OpenTransactionInfo { get; }

		public string DateTime => TransactionDetails.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		public bool Confirmed => Confirmations > 0;

		public int Confirmations => TransactionDetails.Confirmations;

		public string AmountBtc => TransactionDetails.AmountBtc;

		public Money Amount => Money.TryParse(TransactionDetails.AmountBtc, out Money money) ? money : Money.Zero;

		public string Label => TransactionDetails.Label;

		public int BlockHeight => TransactionDetails.BlockHeight;

		public string TransactionId => TransactionDetails.TransactionId;

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

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(AmountBtc));
			this.RaisePropertyChanged(nameof(TransactionId));
			this.RaisePropertyChanged(nameof(DateTime));
			this.RaisePropertyChanged(nameof(Label));
		}

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
