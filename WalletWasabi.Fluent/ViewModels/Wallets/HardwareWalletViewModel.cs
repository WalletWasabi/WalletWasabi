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
				var file = await FileDialogHelper.OpenFileAsync(Lang.Resources.HardwareWalletViewModel_ImportTransaction_Title, new[] { "psbt", "txn", "*" });
				if (file is { })
				{
					var path = file.Path.LocalPath;
					var txn = await walletModel.Transactions.LoadFromFileAsync(path);
					Navigate().To().BroadcastTransaction(txn);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(
					Lang.Resources.HardwareWalletViewModel_ImportTransaction_Title,
					ex.ToUserFriendlyString(),
					Lang.Resources.HardwareWalletViewModel_Error_LoadTransaction_Caption);
			}
		});
	}
}
