using System;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(Wallet wallet) : base(wallet)
		{
			PsbtWorkflowEnabled = Services.UiConfig.UsePsbtWorkflow;

			BroadcastPsbtCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
                {
                	var path = await FileDialogHelper.ShowOpenFileDialogAsync("Import Transaction", new[] { "psbt", "*" });
                	if (path is { })
                	{
                		var txn = await TransactionHelpers.ParseTransactionAsync(path, wallet.Network);
                        Navigate(NavigationTarget.DialogScreen).To(new BroadcastTransactionViewModel(wallet.Network, txn));
                    }
                }
                catch (Exception ex)
                {
                	Logger.LogError(ex);
                	await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "It was not possible to load the transaction.");
                }
			});
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Services.UiConfig.WhenAnyValue(x => x.UsePsbtWorkflow)
				.Subscribe(value => PsbtWorkflowEnabled = value)
				.DisposeWith(disposables);
		}
	}
}
