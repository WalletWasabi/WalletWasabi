using System;
using System.Reactive.Linq;
using System.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	[NavigationMetaData(Title = "Enter your password")]
	public partial class HardwareWalletAuthDialogViewModel : DialogViewModelBase<SmartTransaction?>
	{
		public HardwareWalletAuthDialogViewModel(Wallet wallet, BuildTransactionResult buildTransactionResult)
		{
			var canExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back), canExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel), canExecute);
			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				// Dequeue any coin-joining coins.
				await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

				// If it's a hardware wallet and still has a private key then it's password.
				if (wallet.KeyManager.IsHardwareWallet && !buildTransactionResult.Signed)
				{
					try
					{
						var client = new HwiClient(wallet.Network);
						using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

						var signedPsbt = await client.SignTxAsync(
								wallet.KeyManager.MasterFingerprint!.Value,
								buildTransactionResult.Psbt,
								cts.Token);

						var signedTransaction = signedPsbt.ExtractSmartTransaction(buildTransactionResult.Transaction);

						Close(DialogResultKind.Normal, signedTransaction);
					}
					catch (Exception ex)
					{
						await ShowErrorAsync("Hardware wallet", ex.ToUserFriendlyString(), "Wasabi was unable to sign your transaction");
						Close();
					}
				}
			}, canExecute);

			EnableAutoBusyOn(NextCommand);
		}
	}
}
