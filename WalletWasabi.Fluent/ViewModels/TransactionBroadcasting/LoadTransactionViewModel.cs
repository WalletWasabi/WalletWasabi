using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	[NavigationMetaData(
		Title = "Broadcaster",
		Caption = "Broadcast your transactions here",
		IconName = "live_regular",
		Order = 5,
		Category = "General",
		Keywords = new[] { "Transaction Id", "Input", "Output", "Amount", "Network", "Fee", "Count", "BTC", "Signed", "Paste", "Import", "Broadcast", "Transaction", },
		NavBarPosition = NavBarPosition.None)]
	public partial class LoadTransactionViewModel : RoutableViewModel
	{
		 [AutoNotify] private SmartTransaction? _finalTransaction;

		public LoadTransactionViewModel()
		{
			// TODO: Remove global
			var global = Locator.Current.GetService<Global>();

			Network = global.Network;
			BitcoinStore = global.BitcoinStore;

			this.WhenAnyValue(x => x.FinalTransaction)
				.Where(x => x is { })
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					try
					{
						Navigate().To(new BroadcastTransactionViewModel(BitcoinStore, x!, Network, global.TransactionBroadcaster));
					}
					catch (Exception ex)
					{
						// TODO: Notify the user
						Logger.LogError(ex);
					}
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

		public BitcoinStore BitcoinStore { get; }

		private Network Network { get; }

		public override NavigationTarget DefaultTarget => NavigationTarget.DialogScreen;

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