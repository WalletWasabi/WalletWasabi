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

			// A Trezor coinjoin wallet signs everything through the bridge: one device connection for
			// sends and coinjoin alike, no USB handover with HWI and no lost coinjoin authorization.
			PSBT signedPsbt = Wallet.KeyManager.IsTrezorCoinJoinWallet()
				? await SignWithTrezorAsync(transactionAuthorizationInfo, cts.Token)
				: await SignWithHwiAsync(transactionAuthorizationInfo.Psbt, cts.Token);

			transactionAuthorizationInfo.Transaction = signedPsbt.ExtractSmartTransaction(transactionAuthorizationInfo.Transaction);

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
	}

	/// <summary>
	/// Signs through HWI, which needs the USB device for itself: the Trezor Bridge we may have started
	/// for coinjoin is stopped first and started again right after, so a running (or auto-restarting)
	/// coinjoin finds the bridge back without the user doing anything. The device forgets its coinjoin
	/// authorization when its session ends, so the next coinjoin start asks for a new hold-to-confirm.
	/// </summary>
	private async Task<PSBT> SignWithHwiAsync(PSBT psbt, CancellationToken cancellationToken)
	{
		// Only a Trezor is served by the bridge; signing with other vendors must not disturb it.
		bool isTrezor = Helpers.WalletHelpers.GetType(Wallet.KeyManager) == WalletType.Trezor;
		bool restartBridge = Wallet.KeyManager.IsTrezorCoinJoinWallet();
		if (isTrezor)
		{
			TrezorBridgeManager.StopIfOurs();
		}
		try
		{
			return await new HwiClient(Wallet.Network).SignTxAsync(
				Wallet.KeyManager.MasterFingerprint!.Value,
				psbt,
				cancellationToken);
		}
		finally
		{
			if (restartBridge)
			{
				await TrezorBridgeManager.EnsureRunningAsync(CancellationToken.None);
			}
		}
	}

	/// <summary>
	/// Signs through the Trezor Bridge: segwit account coins as regular witness spends (the device asks
	/// for the previous transactions to verify the amounts) and SLIP-25 coinjoin account coins as taproot
	/// spends behind an UnlockPath. The user confirms the outputs on the device as usual. Coin selection
	/// guarantees one transaction never mixes the two accounts.
	/// </summary>
	private async Task<PSBT> SignWithTrezorAsync(TransactionAuthorizationInfo transactionAuthorizationInfo, CancellationToken cancellationToken)
	{
		var psbt = transactionAuthorizationInfo.Psbt;
		var transaction = psbt.GetGlobalTransaction();
		bool spendsCoinJoinAccount = false;

		var inputs = psbt.Inputs
			.Select((input, index) =>
			{
				var keyPath = input.WitnessUtxo is { } utxo ? Wallet.KeyManager.TryGetKeyPath(utxo.ScriptPubKey) : null;
				if (keyPath is null)
				{
					throw new InvalidOperationException("Cannot sign an input that does not belong to this wallet.");
				}

				bool isSlip25 = keyPath.IsSlip25KeyPath();
				spendsCoinJoinAccount |= isSlip25;

				return new TrezorTxInput
				{
					AddressN = keyPath.Indexes,
					PrevHash = input.PrevOut.Hash.ToBytes(lendian: false),
					PrevIndex = input.PrevOut.N,
					Sequence = transaction.Inputs[index].Sequence.Value,
					ScriptType = isSlip25 ? TrezorInputScriptType.SpendTaproot : TrezorInputScriptType.SpendWitness,
					Amount = (ulong)input.WitnessUtxo!.Value.Satoshi,
				};
			})
			.ToList();

		// Own outputs are sent as key paths so the device can verify them, but only when their account
		// matches the unlock state: without the SLIP-25 unlock the firmware rejects SLIP-25 output paths
		// ("Forbidden key path"), and with it, it rejects segwit output paths. Cross-account transfers
		// (funding the coinjoin account or sweeping it back) are therefore shown as plain address outputs
		// that the user verifies on the device screen.
		var outputs = psbt.Outputs
			.Select(output =>
			{
				var keyPath = Wallet.KeyManager.TryGetKeyPath(output.ScriptPubKey);
				bool isSlip25 = keyPath?.IsSlip25KeyPath() is true;
				bool verifiableByPath = keyPath is not null && isSlip25 == spendsCoinJoinAccount;
				return new TrezorTxOutput
				{
					AddressN = verifiableByPath ? keyPath!.Indexes : [],
					Address = verifiableByPath ? "" : output.ScriptPubKey.GetDestinationAddress(Wallet.Network)!.ToString(),
					Amount = (ulong)output.Value.Satoshi,
					ScriptType = !verifiableByPath
						? TrezorOutputScriptType.PayToAddress
						: isSlip25
							? TrezorOutputScriptType.PayToTaproot
							: TrezorOutputScriptType.PayToWitness,
				};
			})
			.ToList();

		// The device verifies the spent amount of every non-taproot input against its previous transaction.
		var previousTransactions = transactionAuthorizationInfo.Transaction.WalletInputs
			.Select(coin => coin.Transaction.Transaction)
			.DistinctBy(tx => tx.GetHash())
			.ToDictionary(tx => tx.GetHash(), tx => tx);

		using var device = await TrezorDevice.FindAsync(Wallet.KeyManager.MasterFingerprint, cancellationToken);
		var signatures = await device.SignTransactionAsync(
			inputs,
			outputs,
			(uint)transaction.Version,
			transaction.LockTime.Value,
			Wallet.Network,
			unlockCoinJoinAccount: spendsCoinJoinAccount,
			previousTransactions,
			cancellationToken);

		var signedPsbt = psbt.Clone();
		foreach (var signature in signatures)
		{
			var index = signature.Key;
			signedPsbt.Inputs[index].FinalScriptWitness = inputs[index].ScriptType == TrezorInputScriptType.SpendTaproot
				? new WitScript(Op.GetPushOp(signature.Value))
				: BuildSegwitWitness(psbt.Inputs[index], signature.Value);
		}

		return signedPsbt;
	}

	/// <summary>A P2WPKH witness is the DER signature (with sighash byte) followed by the public key.</summary>
	private WitScript BuildSegwitWitness(PSBTInput input, byte[] signature)
	{
		if (!Wallet.KeyManager.TryGetKeyForScriptPubKey(input.WitnessUtxo!.ScriptPubKey, out var hdPubKey))
		{
			throw new InvalidOperationException("Cannot find the public key of a signed input.");
		}

		// The device returns the DER signature without the sighash type byte.
		byte[] signatureWithSighash = [.. signature, (byte)SigHash.All];
		return new WitScript(Op.GetPushOp(signatureWithSighash), Op.GetPushOp(hdPubKey.PubKey.ToBytes()));
	}
}
