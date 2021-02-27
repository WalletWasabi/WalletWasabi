using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	[NavigationMetaData(Title = "Hardware Wallet")]
	public partial class HardwareWalletAuthDialogViewModel : AuthorizationDialogBase
	{
		private readonly Wallet _wallet;
		private readonly BuildTransactionResult _buildTransactionResult;

		public HardwareWalletAuthDialogViewModel(Wallet wallet, BuildTransactionResult buildTransactionResult)
		{
			_wallet = wallet;
			_buildTransactionResult = buildTransactionResult;
		}

		protected override async Task Authorize()
		{
			// Dequeue any coin-joining coins.
			await _wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

			// If it's a hardware wallet and still has a private key then it's password.
			if (_wallet.KeyManager.IsHardwareWallet && !_buildTransactionResult.Signed)
			{
				try
				{
					var client = new HwiClient(_wallet.Network);
					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var signedPsbt = await client.SignTxAsync(
						_wallet.KeyManager.MasterFingerprint!.Value,
						_buildTransactionResult.Psbt,
						cts.Token);

					var signedTransaction = signedPsbt.ExtractSmartTransaction(_buildTransactionResult.Transaction);

					Close(DialogResultKind.Normal, signedTransaction);
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Hardware wallet", ex.ToUserFriendlyString(), "Wasabi was unable to sign your transaction");
					Close();
				}
			}
		}
	}
}
