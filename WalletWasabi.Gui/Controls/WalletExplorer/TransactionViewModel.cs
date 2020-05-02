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

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase
	{
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;

		public TransactionViewModel(TransactionInfo model)
		{
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			CopyTransactionId = ReactiveCommand.CreateFromTask(TryCopyTxIdToClipboardAsync);

			OpenTransactionInfo = ReactiveCommand.Create(() =>
			{
				var shell = IoC.Get<IShell>();

				var transactionInfo = shell.Documents?.OfType<TransactionInfoTabViewModel>()?.FirstOrDefault(x => x.Transaction?.TransactionId == TransactionId);

				if (transactionInfo is null)
				{
					transactionInfo = new TransactionInfoTabViewModel(Model);
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

		private TransactionInfo Model { get; }

		public ReactiveCommand<Unit, Unit> CopyTransactionId { get; }

		public ReactiveCommand<Unit, Unit> OpenTransactionInfo { get; }

		public string DateTime => Model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		public bool Confirmed => Model.Confirmed;

		public int Confirmations => Model.Confirmations;

		public string AmountBtc => Model.AmountBtc;

		public Money Amount => Money.TryParse(Model.AmountBtc, out Money money) ? money : Money.Zero;

		public string Label => Model.Label;

		public int BlockHeight => Model.BlockHeight;

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

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(AmountBtc));
			this.RaisePropertyChanged(nameof(TransactionId));
			this.RaisePropertyChanged(nameof(DateTime));
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
