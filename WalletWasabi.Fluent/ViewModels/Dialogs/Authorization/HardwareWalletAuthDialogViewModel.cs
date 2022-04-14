using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Hwi;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Authorize with Hardware Wallet", NavigationTarget = NavigationTarget.CompactDialogScreen)]
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
		WalletType = WalletHelpers.GetType(wallet.KeyManager);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";
	}

	public WalletType WalletType { get; }

	protected override async Task<bool> AuthorizeAsync()
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
