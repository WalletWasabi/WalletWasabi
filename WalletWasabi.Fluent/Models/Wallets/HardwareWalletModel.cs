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
	public HardwareWalletModel(Wallet wallet, AmountProvider amountProvider) : base(wallet, amountProvider)
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

			TimeSpan baseTimeout = TimeSpan.FromMinutes(3);

			// Define the additional timeout increment as a TimeSpan for every 10 inputs.
			TimeSpan additionalTimeoutPer10Inputs = TimeSpan.FromMinutes(1);
			int inputCount = transactionAuthorizationInfo.Transaction.WalletInputs.Count;

			TimeSpan totalTimeout = baseTimeout + TimeSpan.FromMinutes((inputCount / 10) * additionalTimeoutPer10Inputs.TotalMinutes);
			using var cts = new CancellationTokenSource(totalTimeout);

			var signedPsbt = await client.SignTxAsync(
				Wallet.KeyManager.MasterFingerprint!.Value,
				transactionAuthorizationInfo.Psbt,
				cts.Token);

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
