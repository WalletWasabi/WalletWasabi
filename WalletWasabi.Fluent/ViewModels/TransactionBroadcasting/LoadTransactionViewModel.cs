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
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

[NavigationMetaData(Title = "Transaction Broadcaster")]
public partial class LoadTransactionViewModel : DialogViewModelBase<SmartTransaction?>
{
	[AutoNotify] private SmartTransaction? _finalTransaction;

	public LoadTransactionViewModel(Network network)
	{
		Network = network;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		this.WhenAnyValue(x => x.FinalTransaction)
			.Where(x => x is { })
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(finalTransaction => Close(result: finalTransaction));

		ImportTransactionCommand = ReactiveCommand.CreateFromTask(
			async () => await OnImportTransactionAsync(),
			outputScheduler: RxApp.MainThreadScheduler);

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
	}

	private async Task OnImportTransactionAsync()
	{
		try
		{
			var path = await FileDialogHelper.ShowOpenFileDialogAsync("Import Transaction", new[] { "psbt", "*" });
			if (path is { })
			{
				FinalTransaction = await TransactionHelpers.ParseTransactionAsync(path, Network);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "It was not possible to load the transaction.");
		}
	}

	private async Task OnPasteAsync()
	{
		try
		{
			if (Application.Current is { Clipboard: { } clipboard })
			{
				var textToPaste = await clipboard.GetTextAsync();

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
					FinalTransaction =
						new SmartTransaction(Transaction.Parse(textToPaste, Network), Height.Unknown);
				}
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
}
