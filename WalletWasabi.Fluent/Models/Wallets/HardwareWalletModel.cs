using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.Wallets;

internal class HardwareWalletModel : WalletModel, IHardwareWalletModel
{
	public HardwareWalletModel(Wallet wallet, IAmountProvider amountProvider) : base(wallet, amountProvider)
	{
		if (!wallet.KeyManager.IsHardwareWallet)
		{
			throw new InvalidOperationException($"Wallet '{wallet.WalletName}' is not a hardware wallet. Cannot initialize instance of type HardwareWalletModel.");
		}
	}

	public async Task<bool> AuthorizeTransactionAsync(TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		try
		{
			var client = new HwiClient(Wallet.Network);

			int baseTimeoutMinutes = 3;
			int additionalTimeoutPer10Inputs = 1; // Example: 1 minute extra for every 10 inputs
			int inputCount = transactionAuthorizationInfo.Transaction.WalletInputs.Count;

			// Calculate total timeout
			int totalTimeoutMinutes = baseTimeoutMinutes + inputCount / 10 * additionalTimeoutPer10Inputs;

			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(totalTimeoutMinutes));

			var signedPsbt = await client.SignTxAsync(
				Wallet.KeyManager.MasterFingerprint!.Value,
				transactionAuthorizationInfo.Psbt,
				cts.Token);

			transactionAuthorizationInfo.Transaction = signedPsbt.ExtractSmartTransaction(transactionAuthorizationInfo.Transaction);

			return true;
		}
		catch (Exception ex)
		{
			//TODO: In Coldcard case, the error could be to higher fee rate, so we should ask the user to lower it.
			Logger.LogError(ex);
			return false;
		}
	}
}
