using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	[NavigationMetaData(Title = "Hardware Wallet")]
	public partial class HardwareWalletAuthDialogViewModel : AuthorizationDialogBase
	{
		private readonly Wallet _wallet;
		private readonly TransactionAuthorizationInfo _transactionAuthorizationInfo;

		public HardwareWalletAuthDialogViewModel(Wallet wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
		{
			if (!wallet.KeyManager.IsHardwareWallet)
			{
				throw new InvalidOperationException("Wallet is not a hardware wallet.");
			}

			_wallet = wallet;
			_transactionAuthorizationInfo = transactionAuthorizationInfo;
			WalletIcon = _wallet.KeyManager.Icon;
		}

		public string? WalletIcon { get; }

		public bool IsHardwareWallet => true;

		protected override async Task Authorize()
		{
			// Dequeue any coin-joining coins.
			await _wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

			// If it's a hardware wallet and still has a private key then it's password.
			if (!_transactionAuthorizationInfo.BuildTransactionResult.Signed)
			{
				try
				{
					var client = new HwiClient(_wallet.Network);
					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var signedPsbt = await client.SignTxAsync(
						_wallet.KeyManager.MasterFingerprint!.Value,
						_transactionAuthorizationInfo.BuildTransactionResult.Psbt,
						cts.Token);

					_transactionAuthorizationInfo.Transaction = signedPsbt.ExtractSmartTransaction(_transactionAuthorizationInfo.Transaction);

					Close(DialogResultKind.Normal, true);
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
