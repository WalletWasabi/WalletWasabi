using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Wallets;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.Models.Wallets;

internal class HardwareWalletModel : WalletModel, IHardwareWalletModel
{
	public HardwareWalletModel(IServices services, Wallet wallet, AmountProvider amountProvider) : base(services, wallet, amountProvider)
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
			TimeSpan baseTimeout = TimeSpan.FromMinutes(3);

			// Define the additional timeout increment as a TimeSpan for every 10 inputs.
			TimeSpan additionalTimeoutPer10Inputs = TimeSpan.FromMinutes(1);
			int inputCount = transactionAuthorizationInfo.Transaction.WalletInputs.Count;

			TimeSpan totalTimeout = baseTimeout + TimeSpan.FromMinutes((inputCount / 10) * additionalTimeoutPer10Inputs.TotalMinutes);
			using var cts = new CancellationTokenSource(totalTimeout);

			PSBT signedPsbt = SpendsSlip25Coins(transactionAuthorizationInfo.Psbt)
				? await SignWithTrezorAsync(transactionAuthorizationInfo.Psbt, cts.Token)
				: await new HwiClient(Wallet.Network).SignTxAsync(
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

	private bool SpendsSlip25Coins(PSBT psbt) =>
		Wallet.KeyManager.IsTrezorCoinJoinWallet()
		&& psbt.Inputs.Any(input => input.WitnessUtxo is { } utxo && Wallet.KeyManager.TryGetKeyPath(utxo.ScriptPubKey)?.IsSlip25KeyPath() is true);

	/// <summary>
	/// Spends from the SLIP-25 coinjoin account, which HWI cannot unlock, so the device is driven
	/// directly through the Trezor Bridge. The user confirms the outputs on the device as usual.
	/// </summary>
	private async Task<PSBT> SignWithTrezorAsync(PSBT psbt, CancellationToken cancellationToken)
	{
		var transaction = psbt.GetGlobalTransaction();
		var inputs = psbt.Inputs
			.Select((input, index) =>
			{
				var keyPath = input.WitnessUtxo is { } utxo ? Wallet.KeyManager.TryGetKeyPath(utxo.ScriptPubKey) : null;
				if (keyPath?.IsSlip25KeyPath() is not true)
				{
					// The authorization unlocks only the coinjoin account, other inputs cannot be signed in the same transaction.
					throw new InvalidOperationException("Coinjoin account coins cannot be spent together with other coins. Select only coinjoin account coins.");
				}

				return new TrezorTxInput
				{
					AddressN = keyPath.Indexes,
					PrevHash = input.PrevOut.Hash.ToBytes(lendian: false),
					PrevIndex = input.PrevOut.N,
					Sequence = transaction.Inputs[index].Sequence.Value,
					ScriptType = TrezorInputScriptType.SpendTaproot,
					Amount = (ulong)input.WitnessUtxo!.Value.Satoshi,
				};
			})
			.ToList();

		// Outputs outside the SLIP-25 account (payments and segwit change) are shown on the device as regular outputs.
		var outputs = psbt.Outputs
			.Select(output =>
			{
				var keyPath = Wallet.KeyManager.TryGetKeyPath(output.ScriptPubKey);
				bool isSlip25 = keyPath?.IsSlip25KeyPath() is true;
				return new TrezorTxOutput
				{
					AddressN = isSlip25 ? keyPath!.Indexes : [],
					Address = isSlip25 ? "" : output.ScriptPubKey.GetDestinationAddress(Wallet.Network)!.ToString(),
					Amount = (ulong)output.Value.Satoshi,
					ScriptType = isSlip25 ? TrezorOutputScriptType.PayToTaproot : TrezorOutputScriptType.PayToAddress,
				};
			})
			.ToList();

		using var device = await TrezorDevice.FindAsync(Wallet.KeyManager.MasterFingerprint, cancellationToken);
		var signatures = await device.SignTransactionAsync(inputs, outputs, (uint)transaction.Version, transaction.LockTime.Value, Wallet.Network, cancellationToken);

		var signedPsbt = psbt.Clone();
		foreach (var signature in signatures)
		{
			signedPsbt.Inputs[signature.Key].FinalScriptWitness = new WitScript(Op.GetPushOp(signature.Value));
		}

		return signedPsbt;
	}
}
