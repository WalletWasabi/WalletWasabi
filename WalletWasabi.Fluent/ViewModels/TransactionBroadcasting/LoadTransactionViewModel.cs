using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

[NavigationMetaData(Title = "LoadTransactionViewModel_Title")]

public partial class LoadTransactionViewModel : DialogViewModelBase<SmartTransaction?>
{
	[AutoNotify] private SmartTransaction? _finalTransaction;

	public LoadTransactionViewModel(UiContext uiContext)
	{
		UiContext = uiContext;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		this.WhenAnyValue(x => x.FinalTransaction)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(finalTransaction => Close(result: finalTransaction));

		ImportTransactionCommand = ReactiveCommand.CreateFromTask(OnImportTransactionAsync, outputScheduler: RxApp.MainThreadScheduler);

		PasteCommand = ReactiveCommand.CreateFromTask(OnPasteAsync);
	}

	public ICommand PasteCommand { get; }

	public ICommand ImportTransactionCommand { get; }

	private async Task OnImportTransactionAsync()
	{
		try
		{
			var file = await FileDialogHelper.OpenFileAsync(Lang.Resources.LoadTransactionViewModel_ImportTransaction_Title, new[] { "psbt", "txn", "*" });
			if (file is { })
			{
				var filePath = file.Path.LocalPath;
				FinalTransaction = await UiContext.TransactionBroadcaster.LoadFromFileAsync(filePath);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(
				Lang.Resources.LoadTransactionViewModel_Title,
				ex.ToUserFriendlyString(),
				Lang.Resources.LoadTransactionViewModel_Error_LoadTransaction_Caption);
		}
	}

	private async Task OnPasteAsync()
	{
		try
		{
			var textToPaste = await UiContext.Clipboard.GetTextAsync();

			if (string.IsNullOrWhiteSpace(textToPaste))
			{
				throw new InvalidDataException("The clipboard is empty!");
			}

			FinalTransaction = UiContext.TransactionBroadcaster.Parse(textToPaste);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(
				Lang.Resources.LoadTransactionViewModel_Title,
				ex.ToUserFriendlyString(),
				Lang.Resources.LoadTransactionViewModel_Error_PasteTransaction_Caption);
		}
	}
}
