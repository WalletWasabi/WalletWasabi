using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	[NavigationMetaData(Title = "Transaction Broadcaster")]
	public partial class LoadTransactionViewModel : DialogViewModelBase<SmartTransaction?>
	{
		[AutoNotify] private SmartTransaction? _finalTransaction;

		public LoadTransactionViewModel(Network network)
		{
			Network = network;

			EnableCancel = true;

			EnableBack = false;

			this.WhenAnyValue(x => x.FinalTransaction)
				.Where(x => x is { })
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(finalTransaction => Close(result: finalTransaction));

			ImportTransactionCommand = ReactiveCommand.CreateFromTask(
				async () => await OnImportTransaction(),
				outputScheduler: RxApp.MainThreadScheduler);

			PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPaste());
		}

		private async Task OnImportTransaction()
		{
			try
			{
				var path = await FileDialogHelper.ShowOpenFileDialogAsync("Import Transaction", new[] { "psbt", "*" });
				if (path is { })
				{
					FinalTransaction = await ParseTransactionAsync(path);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "It was not possible to load the transaction.");
			}
		}

		private async Task OnPaste()
		{
			try
			{
				var textToPaste = await Application.Current.Clipboard.GetTextAsync();

				if (string.IsNullOrWhiteSpace(textToPaste))
				{
					throw new InvalidDataException("The clipboard is empty!");
				}

				if (PSBT.TryParse(textToPaste, Network, out var signedPsbt))
				{
					if (!signedPsbt.IsAllFinalized())
					{
						signedPsbt.Finalize();
					}

					FinalTransaction = signedPsbt.ExtractSmartTransaction();
				}
				else
				{
					FinalTransaction = new SmartTransaction(Transaction.Parse(textToPaste, Network), Models.Height.Unknown);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "It was not possible to paste the transaction.");
			}
		}

		private Network Network { get; }

		public ICommand PasteCommand { get; }

		public ICommand ImportTransactionCommand { get; }

		private async Task<SmartTransaction> ParseTransactionAsync(string path)
		{
			var psbtBytes = await File.ReadAllBytesAsync(path);
			PSBT psbt;

			try
			{
				psbt = PSBT.Load(psbtBytes, Network);
			}
			catch
			{
				var text = await File.ReadAllTextAsync(path);
				text = text.Trim();
				try
				{
					psbt = PSBT.Parse(text, Network);
				}
				catch
				{
					return new SmartTransaction(Transaction.Parse(text, Network), Models.Height.Unknown);
				}
			}

			if (!psbt.IsAllFinalized())
			{
				psbt.Finalize();
			}

			return psbt.ExtractSmartTransaction();
		}
	}
}
