using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Wallets;

/// <summary>
/// Every operation that talks to a hardware wallet. A device is reachable over two mutually exclusive transports,
/// HWI (which takes the USB device for itself) and the Trezor Bridge (the only way to a SLIP-25 coinjoin account);
/// choosing between them and handing the device over is this service's business, not its callers'.
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

	/// <summary>Most coinjoin rounds one device authorization may cover; the firmware refuses more under its own safety checks.</summary>
	public const int MaxAuthorizationRounds = 500;

	/// <summary>Highest mining fee rate (sat/vByte) a device may be authorized to sign coinjoins at; above this a cap is no cap.</summary>
	public const decimal MaxAuthorizationFeeRate = 10_000m;

	/// <summary>Whether the number of rounds is one a device can be asked to approve; the reason is written for a settings field to show as is.</summary>
	public static bool TryValidateMaxRounds(int? maxRounds, [NotNullWhen(false)] out string? error)
	{
		if (maxRounds is < 1 or > MaxAuthorizationRounds)
		{
			error = $"Must be a whole number between 1 and {MaxAuthorizationRounds}.";
			return false;
		}

		error = null;
		return true;
	}

	/// <summary>Whether the mining fee rate is one a device can be authorized to sign at, with the reason when it is not.</summary>
	public static bool TryValidateMaxMiningFeeRate(decimal? maxMiningFeeRate, [NotNullWhen(false)] out string? error)
	{
		if (maxMiningFeeRate is <= 0m or > MaxAuthorizationFeeRate)
		{
			error = $"Must be a fee rate above 0 and at most {MaxAuthorizationFeeRate} sat/vByte.";
			return false;
		}

		error = null;
		return true;
	}

	/// <summary>How long a device may take to sign a transaction: a person confirms every output on its screen.</summary>
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

	/// <summary>Lists the connected devices. Releases a bridge we own first, since HWI needs the device itself.</summary>
	public async Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancellationToken)
	{
		_bridge.StopIfOurs();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		var detectedHardwareWallets = (await new HwiClient(_network).EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).ToArray();

		cancellationToken.ThrowIfCancellationRequested();

		return detectedHardwareWallets;
	}

	/// <summary>Runs the device's initial setup, for a device that reports it has no seed yet.</summary>
	public async Task InitializeAsync(HwiEnumerateEntry device, CancellationToken cancellationToken)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
		using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		// Trezor T doesn't require interactive mode.
		var interactiveMode = !(device.Model == HardwareWalletModels.Trezor_T || device.Model == HardwareWalletModels.Trezor_T_Simulator);

		try
		{
			await new HwiClient(_network).SetupAsync(device.Model, device.Path, interactiveMode, initCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	/// <summary>Imports an already detected device as a watch-only wallet.</summary>
	/// <param name="enableCoinjoin">Also read the coinjoin account, so the device can sign coinjoins; the device asks for a confirmation.</param>
	/// <param name="addressToConfirm">Told each address the device is about to show, so the caller can put it next to the device for the user to compare.</param>
	public async Task<KeyManager> ImportAsync(HwiEnumerateEntry device, string walletFilePath, bool enableCoinjoin, IProgress<BitcoinAddress>? addressToConfirm, CancellationToken cancellationToken)
	{
		if (device.Fingerprint is not { } fingerprint)
		{
			throw new InvalidOperationException("The device did not report a master fingerprint.");
		}

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var genCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		if (enableCoinjoin && CanSignCoinJoins(device))
		{
			// Every account is read in one bridge session, so HWI and the bridge do not contend for the device.
			using var bridgeDevice = await AcquireAsync(fingerprint, genCts.Token).ConfigureAwait(false);
			return await ReadAccountsAsync(bridgeDevice, fingerprint, walletFilePath, enableCoinjoin: true, addressToConfirm, genCts.Token).ConfigureAwait(false);
		}

		var segwitExtPubKey = await new HwiClient(_network).GetXpubAsync(device.Model, device.Path, KeyManager.GetAccountKeyPath(_network, ScriptPubKeyType.Segwit), genCts.Token).ConfigureAwait(false);
		var keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, segwitExtPubKey, null, null, null, _network, walletFilePath);
		keyManager.SetIcon(device.WalletType);
		return keyManager;
	}

	/// <summary>Imports the connected device without detecting it over HWI first, which a headless host cannot do.</summary>
	public async Task<KeyManager> ImportConnectedAsync(string walletFilePath, bool enableCoinjoin, IProgress<BitcoinAddress>? addressToConfirm, CancellationToken cancellationToken)
	{
		using var device = await AcquireAsync(masterFingerprint: null, cancellationToken).ConfigureAwait(false);
		var fingerprint = await device.GetMasterFingerprintAsync(cancellationToken).ConfigureAwait(false);
		return await ReadAccountsAsync(device, fingerprint, walletFilePath, enableCoinjoin, addressToConfirm, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Adds a coinjoin account to an imported watch-only wallet; the device confirms it and shows its first address to check, as an import does.</summary>
	public async Task EnableCoinJoinAsync(KeyManager keyManager, IProgress<BitcoinAddress>? addressToConfirm, CancellationToken cancellationToken)
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
		var coinJoinExtPubKey = await device.GetAccountXpubAsync(coinJoinAccountKeyPath, _network, cancellationToken).ConfigureAwait(false);
		await ConfirmAccountOnDeviceAsync(device, coinJoinAccountKeyPath, coinJoinExtPubKey, addressToConfirm, cancellationToken).ConfigureAwait(false);

		keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, coinJoinExtPubKey);
	}

	/// <summary>
	/// Signs with the device: a wallet with a coinjoin account signs everything over the bridge (one device session
	/// for sends and coinjoins alike), every other wallet through HWI, which borrows the device from any bridge we own.
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

		// A Trezor wallet (recognised by its icon) shares the bridge, so HWI has to borrow the device from a bridge
		// of ours; only put that bridge back if we actually took it. The device forgets a coinjoin authorization
		// when its session ends, so the next coinjoin start asks for a new confirmation.
		bool borrowedFromOurBridge = keyManager.Icon is { } icon && Enum.TryParse<WalletType>(icon, ignoreCase: true, out var walletType) && walletType is WalletType.Trezor && _bridge.StopIfOurs();
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

	/// <summary>Shows a receive address on the device screen and verifies that it is the one the wallet expects.</summary>
	public async Task DisplayAddressAsync(KeyManager keyManager, KeyPath fullKeyPath, BitcoinAddress expectedAddress, CancellationToken cancellationToken)
	{
		AssertKeysAreOnADevice(keyManager);
		var fingerprint = keyManager.MasterFingerprint!.Value;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
		try
		{
			if (IsRemoteSigner(keyManager))
			{
				// A coinjoin account address needs the UnlockPath that only the bridge can send, and the bridge
				// holds the device anyway - so both accounts of such a wallet are verified over the bridge.
				using var device = await AcquireAsync(fingerprint, linkedCts.Token).ConfigureAwait(false);
				await ConfirmAddressOnDeviceAsync(device, fullKeyPath, expectedAddress, addressToConfirm: null, linkedCts.Token).ConfigureAwait(false);
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

	/// <summary>Asks the device to authorize a batch of coinjoin rounds (shown with the fee cap, confirmed physically); the returned key chain then signs them without further interaction.</summary>
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
		if (keyChain is not null && !await keyChain.Device.IsSessionAliveAsync(cancellationToken).ConfigureAwait(false))
		{
			// The bridge was restarted, dropped the device, or the wallet was stopped: every call on the old
			// session would fail forever, so let go of it and acquire the device anew.
			Logger.LogInfo("The Trezor bridge session of this wallet is gone, acquiring the device again.");
			keyChain.Dispose();
			keyChain = null;
		}

		if (keyChain is null)
		{
			var device = await AcquireAsync(keyManager.MasterFingerprint, cancellationToken).ConfigureAwait(false);
			keyChain = new TrezorKeyChain(device, keyManager);
		}

		await keyChain.Device
			.AuthorizeCoinJoinAsync(coordinatorIdentifier, maxRounds, maxMiningFeeRate, keyManager.TaprootAccountKeyPath, _network, cancellationToken)
			.ConfigureAwait(false);

		keyChain.MaxMiningFeeRate = maxMiningFeeRate;
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

	/// <summary>Acquires the device, starting a bridge for it first when none is running, so nothing has to be launched by hand.</summary>
	private async Task<TrezorDevice> AcquireAsync(HDFingerprint? masterFingerprint, CancellationToken cancellationToken)
	{
		await _bridge.EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
		return await TrezorDevice.FindAsync(masterFingerprint, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the accounts and has the device show the first address of each before the wallet is written: the bridge is
	/// unauthenticated, so nothing it answers is trusted until the device has shown an address derived from it. A failed check leaves no wallet file.
	/// </summary>
	internal async Task<KeyManager> ReadAccountsAsync(TrezorDevice device, HDFingerprint fingerprint, string walletFilePath, bool enableCoinjoin, IProgress<BitcoinAddress>? addressToConfirm, CancellationToken cancellationToken)
	{
		var segwitAccountKeyPath = KeyManager.GetAccountKeyPath(_network, ScriptPubKeyType.Segwit);
		var segwitExtPubKey = await device.GetAccountXpubAsync(segwitAccountKeyPath, _network, cancellationToken).ConfigureAwait(false);
		await ConfirmAccountOnDeviceAsync(device, segwitAccountKeyPath, segwitExtPubKey, addressToConfirm, cancellationToken).ConfigureAwait(false);

		KeyPath? coinJoinAccountKeyPath = null;
		ExtPubKey? coinJoinExtPubKey = null;
		if (enableCoinjoin)
		{
			coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(_network);
			coinJoinExtPubKey = await device.GetAccountXpubAsync(coinJoinAccountKeyPath, _network, cancellationToken).ConfigureAwait(false);
			await ConfirmAccountOnDeviceAsync(device, coinJoinAccountKeyPath, coinJoinExtPubKey, addressToConfirm, cancellationToken).ConfigureAwait(false);
		}

		// Only coins of the coinjoin account can join rounds, so its addresses are handed out by default; the
		// regular account stays available for deposits that should not be coinjoined.
		var keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, segwitExtPubKey, coinJoinExtPubKey, null, null, _network, walletFilePath, coinJoinAccountKeyPath);
		keyManager.DefaultReceiveScriptType = ScriptPubKeyType.TaprootBIP86;
		keyManager.SetIcon(WalletType.Trezor);
		keyManager.ToFile();
		return keyManager;
	}

	/// <summary>Has the device show the first receive address of an account, derived from the xpub just read the way the wallet derives it.</summary>
	private Task ConfirmAccountOnDeviceAsync(TrezorDevice device, KeyPath accountKeyPath, ExtPubKey accountExtPubKey, IProgress<BitcoinAddress>? addressToConfirm, CancellationToken cancellationToken)
	{
		var fullKeyPath = accountKeyPath.Derive(0).Derive(0);
		var expectedAddress = accountExtPubKey.Derive(0).Derive(0).PubKey.GetAddress(fullKeyPath.GetScriptTypeFromKeyPath(), _network);
		return ConfirmAddressOnDeviceAsync(device, fullKeyPath, expectedAddress, addressToConfirm, cancellationToken);
	}

	/// <summary>Shows the address on the device and checks it against the wallet's. The user comparing the two screens is what proves the keys; this only catches a transport that showed nothing.</summary>
	private async Task ConfirmAddressOnDeviceAsync(TrezorDevice device, KeyPath fullKeyPath, BitcoinAddress expectedAddress, IProgress<BitcoinAddress>? addressToConfirm, CancellationToken cancellationToken)
	{
		addressToConfirm?.Report(expectedAddress);
		var shownAddress = await device.ShowAddressAsync(fullKeyPath, _network, cancellationToken).ConfigureAwait(false);
		if (shownAddress != expectedAddress.ToString())
		{
			throw new HardwareWalletException("The device shows a different address than the wallet. Do not use either of them.");
		}
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
			globalTransaction.Version,
			globalTransaction.LockTime.Value,
			_network,
			unlockCoinJoinAccount: spendsCoinJoinAccount,
			previousTransactions,
			cancellationToken).ConfigureAwait(false);

		var signedPsbt = psbt.Clone();
		foreach (var (index, signature) in signatures)
		{
			signedPsbt.Inputs[index].FinalScriptWitness = inputs[index].ScriptType == TrezorInputScriptType.SpendTaproot
				? new WitScript(Op.GetPushOp(signature))
				: BuildSegwitWitness(keyManager, psbt.Inputs[index], signature);
		}

		AssertSpendsWhatWasBuilt(psbt, signedPsbt);
		return signedPsbt;
	}

	/// <summary>Checks that signing, done by another process, did not change what is spent or where it goes: the user only approved what the device displayed.</summary>
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
