using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	public partial class LoadTransactionViewModel : DialogViewModelBase<SmartTransaction?>
	{
		 [AutoNotify] private SmartTransaction? _finalTransaction;

		public LoadTransactionViewModel(Network network)
		{
			Network = network;

			this.WhenAnyValue(x => x.FinalTransaction)
				.Where(x => x is { })
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(finalTransaction =>
				{
					Close(finalTransaction);
				});

			ImportTransactionCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					try
					{
						var path = await FileDialogHelper.ShowOpenFileDialogAsync("Import Transaction");
						if (path is { })
						{
							FinalTransaction = await ParseTransactionAsync(path);
						}
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				},
				outputScheduler: RxApp.MainThreadScheduler);

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var textToPaste = await Application.Current.Clipboard.GetTextAsync();

					if (string.IsNullOrWhiteSpace(textToPaste))
					{
						// TODO: Clipboard is empty message
						return;
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
					// TODO: Notify the user about the error
					Logger.LogError(ex);
				}
			});
		}

		private Network Network { get; }

		public ICommand PasteCommand { get; }

		public ICommand ImportTransactionCommand { get; }

		private async Task<SmartTransaction> ParseTransactionAsync(string path)
		{
			var psbtBytes = await File.ReadAllBytesAsync(path);
			PSBT? psbt = null;
			Transaction? transaction = null;

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
					transaction = Transaction.Parse(text, Network);
				}
			}

			if (psbt is { })
			{
				if (!psbt.IsAllFinalized())
				{
					psbt.Finalize();
				}

				return psbt.ExtractSmartTransaction();
			}

			return new SmartTransaction(transaction, Models.Height.Unknown);
		}
	}
}