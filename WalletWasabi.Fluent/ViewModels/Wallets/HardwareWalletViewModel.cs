using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class HardwareWalletViewModel : WalletViewModel
{
	internal HardwareWalletViewModel(UiContext uiContext, WalletPageViewModel parent) : base(uiContext, parent)
	{
		BroadcastPsbtCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				var path = await FileDialogHelper.ShowOpenFileDialogAsync("Import Transaction", new[] { "psbt", "txn", "*" });
				if (path is { })
				{
					var txn = await TransactionHelpers.ParseTransactionAsync(path, parent.Wallet.Network);
					Navigate().To().BroadcastTransaction(txn);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "It was not possible to load the transaction.");
			}
		});
	}
}
