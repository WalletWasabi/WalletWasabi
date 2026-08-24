using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Wallets;

/// <summary>
/// Every operation that talks to a hardware wallet, and the ownership of the connections used to do it.
///
/// A device is reachable over two mutually exclusive transports: HWI, which takes the USB device for itself,
/// and the Trezor Bridge, which is the only way to reach a SLIP-25 coinjoin account. Choosing between them and
/// handing the device over is this service's business, not its callers': they ask for an operation on a wallet
/// and get the result. That also keeps the knowledge of which vendor needs what in one place.
/// </summary>
public class HardwareWalletService : IDisposable
{
	/// <summary>Where to get a bridge when none is running. Callers may show this to the user.</summary>
	public static string BridgeDownloadUrl => TrezorBridgeProcess.SuiteDownloadUrl;

	public HardwareWalletService(Network network)
	{
		_network = network;
		_bridge = new TrezorBridgeProcess();
		_bridge.StatusChanged += (_, status) => TransportStatusChanged?.Invoke(this, status);
	}

	private readonly Network _network;
	private readonly TrezorBridgeProcess _bridge;

	/// <summary>Raised when the transport used to reach the device changes, so the UI can show it.</summary>
	public event EventHandler<HardwareWalletTransport>? TransportStatusChanged;

	/// <summary>How the device is currently reached.</summary>
	public HardwareWalletTransport TransportStatus => _bridge.Status;

	/// <summary>Whether a bridge is reachable, to warn before offering coinjoin on an import screen.</summary>
	public Task<bool> IsCoinJoinTransportAvailableAsync(CancellationToken cancellationToken) =>
		TrezorDevice.IsBridgeAvailableAsync(cancellationToken);

	/// <summary>Whether this wallet's coinjoins are signed by a device rather than by keys we hold.</summary>
	public static bool IsRemoteSigner(KeyManager keyManager) => keyManager.IsTrezorCoinJoinWallet();

	/// <summary>
	/// The range of coinjoin rounds a single device authorization may cover. The firmware refuses more than
	/// the upper bound under its own safety checks, and an authorization for no rounds authorizes nothing.
	/// </summary>
	public static (int Min, int Max) AllowedAuthorizationRounds => (1, 500);

	/// <summary>
	/// The range of mining fee rates (sat/vByte) a device may be authorized to sign coinjoins at. A cap of
	/// zero could never be met, and one this far above any plausible fee market would not be a cap at all.
	/// </summary>
	public static (decimal Min, decimal Max) AllowedAuthorizationFeeRates => (0m, 10_000m);

	/// <summary>
	/// Whether the number of rounds is one a device can be asked to approve, with the reason when it is not.
	/// The reason is written for a person, so a settings field can show it as is.
	/// </summary>
	public static bool TryValidateMaxRounds(int? maxRounds, [NotNullWhen(false)] out string? error)
	{
		var (minRounds, maxAllowedRounds) = AllowedAuthorizationRounds;
		if (maxRounds is { } rounds && (rounds < minRounds || rounds > maxAllowedRounds))
		{
			error = $"Must be a whole number between {minRounds} and {maxAllowedRounds}.";
			return false;
		}

		error = null;
		return true;
	}

	/// <summary>Whether the mining fee rate is one a device can be authorized to sign at, with the reason when it is not.</summary>
	public static bool TryValidateMaxMiningFeeRate(decimal? maxMiningFeeRate, [NotNullWhen(false)] out string? error)
	{
		var (minFeeRate, maxAllowedFeeRate) = AllowedAuthorizationFeeRates;
		if (maxMiningFeeRate is { } feeRate && (feeRate <= minFeeRate || feeRate > maxAllowedFeeRate))
		{
			error = $"Must be a fee rate above {minFeeRate} and at most {maxAllowedFeeRate} sat/vByte.";
			return false;
		}

		error = null;
		return true;
	}

	/// <summary>
	/// How long a device may take to sign a transaction. A person reads and confirms every output on the
	/// device screen, which takes longer the more inputs there are.
	/// </summary>
	public static TimeSpan SigningTimeout(int inputCount) =>
		TimeSpan.FromMinutes(3) + TimeSpan.FromMinutes(inputCount / 10);

	/// <summary>How long a device may take to confirm a coinjoin authorization (one hold-to-confirm).</summary>
	public static TimeSpan AuthorizationTimeout => TimeSpan.FromMinutes(3);

	/// <summary>Throws when the limits are outside what a device can be asked to approve.</summary>
	public static void AssertAuthorizationLimits(int? maxRounds, decimal? maxMiningFeeRate)
	{
		if (!TryValidateMaxRounds(maxRounds, out var roundsError))
		{
			throw new ArgumentOutOfRangeException(nameof(maxRounds), maxRounds, roundsError);
		}

		if (!TryValidateMaxMiningFeeRate(maxMiningFeeRate, out var feeRateError))
		{
			throw new ArgumentOutOfRangeException(nameof(maxMiningFeeRate), maxMiningFeeRate, feeRateError);
		}
	}

	/// <summary>Whether a detected device can act as a coinjoin remote signer, to offer it while importing.</summary>
	public static bool CanSignCoinJoins(HwiEnumerateEntry device) => device.Model.SupportsCoinJoin();

	/// <summary>
	/// Whether this wallet's device shares the bridge transport, so that taking the device for an HWI
	/// operation would disturb a coinjoin of another wallet running over a bridge we own.
	/// </summary>
	private static bool SharesBridgeTransport(KeyManager keyManager) =>
		keyManager.Icon is { } icon && Enum.TryParse<WalletType>(icon, ignoreCase: true, out var walletType) && walletType is WalletType.Trezor;

	/// <summary>Lists the connected devices. Releases a bridge we own first, since HWI needs the device itself.</summary>
	public async Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancellationToken)
	{
		_bridge.StopIfOurs();

		var client = new HwiClient(_network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		var detectedHardwareWallets = (await client.EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).ToArray();

		cancellationToken.ThrowIfCancellationRequested();

		return detectedHardwareWallets;
	}

	/// <summary>Runs the device's initial setup, for a device that reports it has no seed yet.</summary>
	public async Task InitializeAsync(HwiEnumerateEntry device, CancellationToken cancellationToken)
	{
		var client = new HwiClient(_network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
		using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		// Trezor T doesn't require interactive mode.
		var interactiveMode = !(device.Model == HardwareWalletModels.Trezor_T || device.Model == HardwareWalletModels.Trezor_T_Simulator);

		try
		{
			await client.SetupAsync(device.Model, device.Path, interactiveMode, initCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	/// <summary>
	/// Imports an already detected device as a watch-only wallet.
	/// </summary>
	/// <param name="enableCoinjoin">
	/// Also read the coinjoin account, so the device can sign coinjoins. Requires a confirmation on the device,
	/// and is only possible for models that can act as a remote signer.
	/// </param>
	public async Task<KeyManager> ImportAsync(HwiEnumerateEntry device, string walletFilePath, bool enableCoinjoin, CancellationToken cancellationToken)
	{
		if (device.Fingerprint is null)
		{
			throw new InvalidOperationException("The device did not report a master fingerprint.");
		}

		var fingerprint = (HDFingerprint)device.Fingerprint;
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var genCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		if (enableCoinjoin && CanSignCoinJoins(device))
		{
			return await ImportOverBridgeAsync(fingerprint, walletFilePath, genCts.Token).ConfigureAwait(false);
		}

		var client = new HwiClient(_network);
		var segwitAccountKeyPath = KeyManager.GetAccountKeyPath(_network, ScriptPubKeyType.Segwit);
		var segwitExtPubKey = await client.GetXpubAsync(device.Model, device.Path, segwitAccountKeyPath, genCts.Token).ConfigureAwait(false);
		var keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, segwitExtPubKey, null, null, null, _network, walletFilePath);
		keyManager.SetIcon(device.WalletType);
		return keyManager;
	}

	/// <summary>
	/// Imports the connected device without detecting it over HWI first, which a headless host cannot do.
	/// Everything is read in one bridge session, so the device is only asked to confirm once.
	/// </summary>
	public async Task<KeyManager> ImportConnectedAsync(string walletFilePath, bool enableCoinjoin, CancellationToken cancellationToken)
	{
		using var device = await AcquireAsync(masterFingerprint: null, cancellationToken).ConfigureAwait(false);
		var fingerprint = await device.GetMasterFingerprintAsync(cancellationToken).ConfigureAwait(false);
		return await ReadAccountsAsync(device, fingerprint, walletFilePath, enableCoinjoin, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Adds a coinjoin account to an already imported watch-only wallet, so it can start signing coinjoins.
	/// Requires a confirmation on the device. No-op if the wallet already has one.
	/// </summary>
	public async Task EnableCoinJoinAsync(KeyManager keyManager, CancellationToken cancellationToken)
	{
		if (!keyManager.IsHardwareWallet)
		{
			throw new InvalidOperationException("Only a hardware wallet can have a coinjoin account added.");
		}
		if (IsRemoteSigner(keyManager))
		{
			return;
		}

		using var device = await AcquireAsync(keyManager.MasterFingerprint, cancellationToken).ConfigureAwait(false);
		var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(_network);
		var coinJoinExtPubKey = await device.GetCoinJoinXpubAsync(coinJoinAccountKeyPath, _network, cancellationToken).ConfigureAwait(false);

		keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, coinJoinExtPubKey);
	}

	/// <summary>
	/// Signs a transaction with the device. A wallet with a coinjoin account signs everything over the bridge,
	/// so that one device session serves sends and coinjoins alike; every other wallet signs through HWI, which
	/// needs the device for itself and therefore borrows it from any bridge we own.
	/// </summary>
	/// <param name="transaction">The transaction being signed; its wallet inputs carry the previous transactions the device asks for.</param>
	public async Task<PSBT> SignTransactionAsync(KeyManager keyManager, PSBT psbt, SmartTransaction transaction, CancellationToken cancellationToken)
	{
		AssertKeysAreOnADevice(keyManager);

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(SigningTimeout(transaction.WalletInputs.Count));
		cancellationToken = timeout.Token;

		if (IsRemoteSigner(keyManager))
		{
			return await SignOverBridgeAsync(keyManager, psbt, transaction, cancellationToken).ConfigureAwait(false);
		}

		// The device forgets a coinjoin authorization when its session ends, so the next coinjoin start asks
		// for a new confirmation; the bridge itself comes back right after signing.
		// Borrow the device from a bridge of ours, and only put that bridge back if we actually took it -
		// starting one for a wallet that does not need it would hold the device for nothing.
		bool borrowedFromOurBridge = SharesBridgeTransport(keyManager) && _bridge.StopIfOurs();
		try
		{
			var signedPsbt = await new HwiClient(_network).SignTxAsync(keyManager.MasterFingerprint!.Value, psbt, cancellationToken).ConfigureAwait(false);
			AssertSpendsWhatWasBuilt(psbt, signedPsbt);
			return signedPsbt;
		}
		finally
		{
			if (borrowedFromOurBridge)
			{
				await _bridge.EnsureRunningAsync(CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Shows a receive address on the device screen and verifies that it is the one the wallet expects, which is
	/// the point of the exercise: an address the host displays alone proves nothing.
	/// </summary>
	public async Task DisplayAddressAsync(KeyManager keyManager, KeyPath fullKeyPath, BitcoinAddress expectedAddress, CancellationToken cancellationToken)
	{
		AssertKeysAreOnADevice(keyManager);
		if (keyManager.MasterFingerprint is not { } fingerprint)
		{
			throw new HardwareWalletException("The wallet has no master fingerprint, so no device can be identified.");
		}

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
		try
		{
			if (IsRemoteSigner(keyManager))
			{
				// A coinjoin account address needs the UnlockPath that only the bridge can send, and the bridge
				// holds the device anyway - so both accounts of such a wallet are verified over the bridge.
				using var device = await AcquireAsync(fingerprint, linkedCts.Token).ConfigureAwait(false);
				var shownAddress = await device.ShowAddressAsync(fullKeyPath, _network, linkedCts.Token).ConfigureAwait(false);
				if (shownAddress != expectedAddress.ToString())
				{
					throw new InvalidOperationException("The device shows a different address than the wallet. Do not use either of them.");
				}
				return;
			}

			await new HwiClient(_network).DisplayAddressAsync(fingerprint, fullKeyPath, linkedCts.Token).ConfigureAwait(false);
		}
		catch (FormatException ex) when (ex.Message.Contains("network") && _network == Network.TestNet)
		{
			// This exception happens every time on TestNet because of Wasabi Keypath handling.
			// The user doesn't need to know about it.
		}
		catch (Exception) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			throw new InvalidOperationException("The device did not answer in time.");
		}
	}

	/// <summary>
	/// Asks the device to authorize a batch of coinjoin rounds. It shows the number of rounds and the maximum
	/// mining fee rate, and the user confirms physically. The returned key chain then produces ownership proofs
	/// and signatures for those rounds without further interaction.
	/// </summary>
	/// <param name="existingKeyChain">The wallet's current key chain, reused when it already holds the device.</param>
	public async Task<IKeyChain> AuthorizeCoinJoinAsync(
		KeyManager keyManager,
		IKeyChain? existingKeyChain,
		string coordinatorIdentifier,
		int maxRounds,
		FeeRate maxMiningFeeRate,
		CancellationToken cancellationToken)
	{
		if (!IsRemoteSigner(keyManager))
		{
			throw new NotSupportedException("This wallet has no coinjoin account, so no device can authorize its coinjoins.");
		}

		var keyChain = existingKeyChain as TrezorKeyChain;
		if (keyChain is null)
		{
			var device = await AcquireAsync(keyManager.MasterFingerprint, cancellationToken).ConfigureAwait(false);
			keyChain = new TrezorKeyChain(device, keyManager);
		}

		try
		{
			await keyChain.Device
				.AuthorizeCoinJoinAsync(coordinatorIdentifier, maxRounds, maxMiningFeeRate, keyManager.TaprootAccountKeyPath, _network, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (TrezorException e)
		{
			throw new HardwareWalletException(e.Message, e);
		}

		return keyChain;
	}

	/// <summary>Makes sure the device of this wallet can be reached, if it needs a transport of ours at all.</summary>
	public async Task EnsureReadyAsync(KeyManager keyManager, CancellationToken cancellationToken)
	{
		if (IsRemoteSigner(keyManager))
		{
			await _bridge.EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Hands the device back, for when this wallet no longer needs it.</summary>
	public void Release(KeyManager keyManager)
	{
		if (IsRemoteSigner(keyManager))
		{
			_bridge.StopIfOurs();
		}
	}

	/// <summary>Guards the operations that only make sense when a device holds the wallet's keys.</summary>
	private static void AssertKeysAreOnADevice(KeyManager keyManager)
	{
		if (!keyManager.IsHardwareWallet)
		{
			throw new HardwareWalletException("The keys of this wallet are not on a device.");
		}
	}

	/// <summary>Acquires the device, starting a transport for it first when we own one.</summary>
	private async Task<TrezorDevice> AcquireAsync(HDFingerprint? masterFingerprint, CancellationToken cancellationToken)
	{
		// Start the bridge if it is not already running, so that coinjoin authorization, signing and reading the
		// coinjoin account work without the user launching anything (and after a detection borrowed the device).
		await _bridge.EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await TrezorDevice.FindAsync(masterFingerprint, cancellationToken).ConfigureAwait(false);
		}
		catch (TrezorBridgeNotFoundException e)
		{
			throw new HardwareWalletTransportNotFoundException(e.Message, e);
		}
		catch (TrezorDeviceNotFoundException e)
		{
			throw new HardwareWalletNotFoundException(e.Message, e);
		}
		catch (TrezorException e)
		{
			throw new HardwareWalletException(e.Message, e);
		}
	}

	private async Task<KeyManager> ImportOverBridgeAsync(HDFingerprint fingerprint, string walletFilePath, CancellationToken cancellationToken)
	{
		// Read the regular account from the bridge in the same session as the coinjoin account, so HWI and the
		// bridge do not contend for the device. If the bridge is unavailable the import fails with a clear error
		// instead of silently dropping coinjoin.
		using var device = await AcquireAsync(fingerprint, cancellationToken).ConfigureAwait(false);
		return await ReadAccountsAsync(device, fingerprint, walletFilePath, enableCoinjoin: true, cancellationToken).ConfigureAwait(false);
	}

	private async Task<KeyManager> ReadAccountsAsync(TrezorDevice device, HDFingerprint fingerprint, string walletFilePath, bool enableCoinjoin, CancellationToken cancellationToken)
	{
		var segwitAccountKeyPath = KeyManager.GetAccountKeyPath(_network, ScriptPubKeyType.Segwit);
		var segwitExtPubKey = await device.GetSegwitAccountXpubAsync(segwitAccountKeyPath, _network, cancellationToken).ConfigureAwait(false);

		KeyManager keyManager;
		if (enableCoinjoin)
		{
			var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(_network);
			var coinJoinExtPubKey = await device.GetCoinJoinXpubAsync(coinJoinAccountKeyPath, _network, cancellationToken).ConfigureAwait(false);
			keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, segwitExtPubKey, coinJoinExtPubKey, null, null, _network, walletFilePath, coinJoinAccountKeyPath);

			// Only coins of the coinjoin account can join rounds, so hand out its addresses by default; the
			// regular account stays available for deposits that should not be coinjoined.
			keyManager.DefaultReceiveScriptType = ScriptPubKeyType.TaprootBIP86;
		}
		else
		{
			keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, segwitExtPubKey, null, null, null, _network, walletFilePath);
		}

		keyManager.SetIcon(WalletType.Trezor);
		return keyManager;
	}

	private async Task<PSBT> SignOverBridgeAsync(KeyManager keyManager, PSBT psbt, SmartTransaction transaction, CancellationToken cancellationToken)
	{
		var globalTransaction = psbt.GetGlobalTransaction();
		bool spendsCoinJoinAccount = false;

		var inputs = psbt.Inputs
			.Select((input, index) =>
			{
				var keyPath = keyManager.TryGetKeyPath(input.WitnessUtxo?.ScriptPubKey ?? throw new InvalidOperationException("Cannot sign an input without its previous output."))
					?? throw new InvalidOperationException("Cannot sign an input that does not belong to this wallet.");

				bool isCoinJoinAccount = keyPath.IsSlip25KeyPath();
				spendsCoinJoinAccount |= isCoinJoinAccount;

				return new TrezorTxInput
				{
					AddressN = keyPath.Indexes,
					PrevHash = input.PrevOut.Hash.ToBytes(lendian: false),
					PrevIndex = input.PrevOut.N,
					Sequence = globalTransaction.Inputs[index].Sequence.Value,
					ScriptType = isCoinJoinAccount ? TrezorInputScriptType.SpendTaproot : TrezorInputScriptType.SpendWitness,
					Amount = (ulong)input.WitnessUtxo!.Value.Satoshi,
				};
			})
			.ToList();

		// An own output is streamed as a verifiable key path only when its account matches the unlock state of
		// the transaction: the device rejects a coinjoin account output path without the unlock, and a regular
		// one with it. A transfer between the two accounts is therefore shown as a plain address to confirm.
		var outputs = psbt.Outputs
			.Select(output =>
			{
				var keyPath = keyManager.TryGetKeyPath(output.ScriptPubKey);
				bool isCoinJoinAccount = keyPath?.IsSlip25KeyPath() is true;
				bool verifiableByPath = keyPath is not null && isCoinJoinAccount == spendsCoinJoinAccount;
				return new TrezorTxOutput
				{
					AddressN = verifiableByPath ? keyPath!.Indexes : [],
					Address = verifiableByPath ? "" : output.ScriptPubKey.GetDestinationAddress(_network)?.ToString()
						?? throw new InvalidOperationException("Cannot show an output that is not an address on the device."),
					Amount = (ulong)output.Value.Satoshi,
					ScriptType = !verifiableByPath
						? TrezorOutputScriptType.PayToAddress
						: isCoinJoinAccount
							? TrezorOutputScriptType.PayToTaproot
							: TrezorOutputScriptType.PayToWitness,
				};
			})
			.ToList();

		// The device verifies the spent amount of every non-taproot input against its previous transaction.
		var previousTransactions = transaction.WalletInputs
			.Select(coin => coin.Transaction.Transaction)
			.DistinctBy(tx => tx.GetHash())
			.ToDictionary(tx => tx.GetHash(), tx => tx);

		using var device = await AcquireAsync(keyManager.MasterFingerprint, cancellationToken).ConfigureAwait(false);
		var signatures = await device.SignTransactionAsync(
			inputs,
			outputs,
			(uint)globalTransaction.Version,
			globalTransaction.LockTime.Value,
			_network,
			unlockCoinJoinAccount: spendsCoinJoinAccount,
			previousTransactions,
			cancellationToken).ConfigureAwait(false);

		var signedPsbt = psbt.Clone();
		foreach (var signature in signatures)
		{
			var index = signature.Key;
			signedPsbt.Inputs[index].FinalScriptWitness = inputs[index].ScriptType == TrezorInputScriptType.SpendTaproot
				? new WitScript(Op.GetPushOp(signature.Value))
				: BuildSegwitWitness(keyManager, psbt.Inputs[index], signature.Value);
		}

		AssertSpendsWhatWasBuilt(psbt, signedPsbt);
		return signedPsbt;
	}

	/// <summary>
	/// Checks that signing did not change what is being spent or where it goes. The signed transaction comes
	/// back from another process and the user only ever approved what their device displayed, so a mismatch
	/// means the transaction about to be broadcast is not the one that was authorized.
	/// </summary>
	public static void AssertSpendsWhatWasBuilt(PSBT built, PSBT signed)
	{
		var before = built.GetGlobalTransaction();
		var after = signed.GetGlobalTransaction();

		bool sameInputs = before.Inputs.Count == after.Inputs.Count
			&& before.Inputs.Select(x => x.PrevOut).SequenceEqual(after.Inputs.Select(x => x.PrevOut));

		bool sameOutputs = before.Outputs.Count == after.Outputs.Count
			&& before.Outputs.Zip(after.Outputs).All(pair => pair.First.Value == pair.Second.Value && pair.First.ScriptPubKey == pair.Second.ScriptPubKey);

		if (!sameInputs || !sameOutputs)
		{
			throw new HardwareWalletException("The signed transaction does not match the one that was built. It was not broadcast.");
		}
	}

	/// <summary>A P2WPKH witness is the DER signature (with sighash byte) followed by the public key.</summary>
	private static WitScript BuildSegwitWitness(KeyManager keyManager, PSBTInput input, byte[] signature)
	{
		if (!keyManager.TryGetKeyForScriptPubKey(input.WitnessUtxo!.ScriptPubKey, out var hdPubKey))
		{
			throw new InvalidOperationException("Cannot find the public key of a signed input.");
		}

		// The device returns the DER signature without the sighash type byte.
		byte[] signatureWithSighash = [.. signature, (byte)SigHash.All];
		return new WitScript(Op.GetPushOp(signatureWithSighash), Op.GetPushOp(hdPubKey.PubKey.ToBytes()));
	}

	public void Dispose()
	{
		_bridge.Dispose();
	}
}
