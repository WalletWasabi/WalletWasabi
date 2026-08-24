using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.Models.Wallets;

internal class HardwareWalletModel : WalletModel, IHardwareWalletModel
{
	private readonly HardwareWalletService _hardwareWallets;

	public HardwareWalletModel(IServices services, Wallet wallet, AmountProvider amountProvider) : base(services, wallet, amountProvider)
	{
		if (!wallet.KeyManager.IsHardwareWallet)
		{
			throw new InvalidOperationException($"Wallet '{wallet.WalletName}' is not a hardware wallet. Cannot initialize instance of type HardwareWalletModel.");
		}

		_hardwareWallets = services.HardwareWallets;
	}

	public async Task<bool> AuthorizeTransactionAsync(TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		try
		{
			// How long the device may take is the service's business: it knows what it is asking of the device.
			PSBT signedPsbt = await _hardwareWallets
				.SignTransactionAsync(Wallet.KeyManager, transactionAuthorizationInfo.Psbt, transactionAuthorizationInfo.Transaction, CancellationToken.None)
				.ConfigureAwait(false);

			transactionAuthorizationInfo.Transaction = signedPsbt.ExtractSmartTransaction(transactionAuthorizationInfo.Transaction);

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
	}
}
