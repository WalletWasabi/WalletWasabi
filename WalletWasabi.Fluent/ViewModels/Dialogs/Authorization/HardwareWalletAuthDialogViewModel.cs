using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Hwi;
using WalletWasabi.Logging;
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
				throw new InvalidOperationException("Not a hardware wallet.");
			}

			_wallet = wallet;
			_transactionAuthorizationInfo = transactionAuthorizationInfo;
			WalletIcon = _wallet.KeyManager.Icon;
		}

		public string? WalletIcon { get; }

		public bool IsHardwareWallet => true;

		protected override async Task<bool> Authorize()
		{
			try
			{
				var client = new HwiClient(_wallet.Network);
				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

				var signedPsbt = await client.SignTxAsync(
					_wallet.KeyManager.MasterFingerprint!.Value,
					_transactionAuthorizationInfo.Psbt,
					cts.Token);

				_transactionAuthorizationInfo.Transaction = signedPsbt.ExtractSmartTransaction(_transactionAuthorizationInfo.Transaction);

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				return false;
			}
		}
	}
}
