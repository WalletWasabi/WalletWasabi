using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class HardwareWalletViewModel : WalletViewModel
{
	internal HardwareWalletViewModel(UiContext uiContext, IWalletModel walletModel, Wallet wallet) : base(uiContext, walletModel, wallet)
	{
		BroadcastPsbtCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				var path = await FileDialogHelper.ShowOpenFileDialogAsync("Import Transaction", new[] { "psbt", "txn", "*" });
				if (path is { })
				{
					var txn = await walletModel.Transactions.LoadFromFileAsync(path);
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
