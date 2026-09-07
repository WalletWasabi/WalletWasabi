using System.Buffers.Binary;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// The Trezor operations HWI cannot do - SLIP-25 account discovery, coinjoin authorization, SLIP-19 ownership
/// proofs and signing - driven directly through the Trezor Bridge.
/// </summary>
public class TrezorDevice : IDisposable
{
	/// <summary>SLIP-25 purpose (10025') dedicated to coinjoin accounts, enforced by the firmware.</summary>
	public const uint Slip25Purpose = 10025 | HardenedIndex;

	private const uint HardenedIndex = 0x80000000;

	/// <summary>First firmware version that accepts coinjoin requests from any coordinator (signature verification against the zkSNACKs key was removed).</summary>
	private static readonly Version MinimumSupportedFirmwareVersion = new(2, 7, 2);

	internal TrezorDevice(TrezorBridgeTransport transport)
	{
		_transport = transport;
	}

	private readonly TrezorBridgeTransport _transport;
	private readonly SemaphoreSlim _lock = new(1, 1);
	private string _bridgeSession = "";
	private bool _useOnDevicePassphrase;
	private bool _disposed;

	public TrezorFeatures? Features { get; private set; }

	/// <summary>Finds and acquires the connected Trezor with the given master fingerprint.</summary>
	public static async Task<TrezorDevice> FindAsync(HDFingerprint? masterFingerprint, CancellationToken cancellationToken)
	{
		var (bridgeUri, bridgeDevices, bridgeError) = await EnumerateAnyBridgeAsync(cancellationToken).ConfigureAwait(false);
		if (bridgeUri is null)
		{
			throw new HardwareWalletTransportNotFoundException($"Trezor Bridge is not running. Start Trezor Suite, which includes the bridge, or download it from {TrezorBridgeProcess.SuiteDownloadUrl} and try again. ({bridgeError})");
		}

		if (bridgeDevices.Count == 0)
		{
			throw new HardwareWalletNotFoundException("No Trezor device found. Connect the Trezor and unlock it with its PIN.");
		}

		string? lastError = null;
		bool sawPassphraseProtectedDevice = false;
		foreach (var bridgeDevice in bridgeDevices)
		{
			// The standard wallet (empty passphrase) is tried first; on-device passphrase entry only when its
			// fingerprint does not match, and the fingerprint check rejects a mistyped passphrase before anything is signed.
			foreach (bool useOnDevicePassphrase in (bool[])[false, true])
			{
				TrezorDevice? device = null;
				try
				{
#pragma warning disable CA2000 // Dispose objects before losing scope - disposed in the finally block or owned by the caller.
					device = new TrezorDevice(new TrezorBridgeTransport(bridgeUri)) { _useOnDevicePassphrase = useOnDevicePassphrase };
#pragma warning restore CA2000
					await device.OpenAsync(bridgeDevice, cancellationToken).ConfigureAwait(false);
					if (masterFingerprint is null || await device.GetMasterFingerprintAsync(cancellationToken).ConfigureAwait(false) == masterFingerprint)
					{
						var foundDevice = device;
						device = null;
						return foundDevice;
					}

					sawPassphraseProtectedDevice |= device.Features?.PassphraseProtection ?? false;
					if (useOnDevicePassphrase || !(device.Features?.PassphraseProtection ?? false))
					{
						break; // Wrong device, or the passphrase entered on the device gives a different wallet.
					}
				}
				catch (TrezorException e)
				{
					lastError = e.Message;
					Logger.LogDebug($"Skipping Trezor device '{bridgeDevice.Path}': {e.Message}");
					break;
				}
				finally
				{
					device?.Dispose();
				}
			}
		}

		string passphraseHint = sawPassphraseProtectedDevice
			? " If this wallet uses a passphrase, enter the exact same passphrase on the device."
			: "";
		throw new HardwareWalletNotFoundException(lastError is null
			? $"No Trezor device with master fingerprint '{masterFingerprint}' found.{passphraseHint}"
			: $"No usable Trezor device found. Last error: {lastError}");
	}

	/// <summary>The first bridge that answers, with the devices it lists; the URI is null when none does.</summary>
	private static async Task<(string? Uri, IReadOnlyList<TrezorBridgeTransport.BridgeDevice> Devices, string? Error)> EnumerateAnyBridgeAsync(CancellationToken cancellationToken)
	{
		string? error = null;
		foreach (string candidateUri in TrezorBridgeTransport.DefaultBridgeUris)
		{
			using var transport = new TrezorBridgeTransport(candidateUri);
			try
			{
				return (candidateUri, await transport.EnumerateAsync(cancellationToken).ConfigureAwait(false), null);
			}
			catch (TrezorException e)
			{
				error = e.Message;
				Logger.LogDebug(e.Message);
			}
		}

		return (null, [], error);
	}

	private async Task OpenAsync(TrezorBridgeTransport.BridgeDevice bridgeDevice, CancellationToken cancellationToken)
	{
		_bridgeSession = await _transport.AcquireAsync(bridgeDevice, cancellationToken).ConfigureAwait(false);

		// A previously canceled call (timeout, user walked away) leaves its reply queued on the bridge,
		// which would offset every following request by one. Drain until the reply to this Initialize.
		var features = await CallRawAsync(TrezorMessages.Initialize(), cancellationToken).ConfigureAwait(false);
		for (int i = 0; features.MessageType != TrezorMessageType.Features && i < 4; i++)
		{
			using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			readCts.CancelAfter(TimeSpan.FromSeconds(5));
			try
			{
				features = await _transport.ReadAsync(_bridgeSession, readCts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				break; // Nothing more queued; fall through to the type check below.
			}
		}
		if (features.MessageType != TrezorMessageType.Features)
		{
			throw UnexpectedMessage(features, TrezorMessageType.Features);
		}
		Features = TrezorFeatures.FromMessage(features);

		if (Features.Model == "1")
		{
			throw new TrezorException("Trezor Model One does not support coinjoin. A Trezor Model T or newer is required.");
		}
		if (Features.Version < MinimumSupportedFirmwareVersion)
		{
			throw new TrezorException($"Trezor firmware {Features.Version} is too old for coinjoin with this coordinator. Version {MinimumSupportedFirmwareVersion} or newer is required.");
		}
	}

	/// <summary>Whether a Trezor Bridge (Trezor Suite or standalone trezord) is reachable, to warn the user before offering coinjoin.</summary>
	public static async Task<bool> IsBridgeAvailableAsync(CancellationToken cancellationToken) =>
		(await EnumerateAnyBridgeAsync(cancellationToken).ConfigureAwait(false)).Uri is not null;

	public async Task<HDFingerprint> GetMasterFingerprintAsync(CancellationToken cancellationToken)
	{
		// Any GetPublicKey response carries the master fingerprint, use a fixed path that needs no unlocking.
		uint[] path = [84 | HardenedIndex, HardenedIndex, HardenedIndex];
		var response = await LockedCallAsync(
			TrezorMessages.GetPublicKey(path, "Bitcoin", TrezorInputScriptType.SpendWitness),
			TrezorMessageType.PublicKey,
			cancellationToken).ConfigureAwait(false);

		var fields = response.ReadFields();
		uint rootFingerprint = (uint)fields[3][0].VarInt;
		byte[] fingerprintBytes = new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(fingerprintBytes, rootFingerprint);
		return new HDFingerprint(fingerprintBytes);
	}

	/// <summary>Gets an account xpub through the bridge; a SLIP-25 path first needs UnlockPath, which the device confirms on screen.</summary>
	public Task<ExtPubKey> GetAccountXpubAsync(KeyPath accountKeyPath, Network network, CancellationToken cancellationToken) =>
		LockedAsync(async () =>
		{
			bool isCoinJoinAccount = accountKeyPath.IsSlip25KeyPath();
			if (isCoinJoinAccount)
			{
				await CallAsync(TrezorMessages.UnlockPath([Slip25Purpose]), TrezorMessageType.UnlockedPathRequest, cancellationToken).ConfigureAwait(false);
			}

			var response = await CallAsync(
				TrezorMessages.GetPublicKey(accountKeyPath.Indexes, GetCoinName(network), isCoinJoinAccount ? TrezorInputScriptType.SpendTaproot : TrezorInputScriptType.SpendWitness),
				TrezorMessageType.PublicKey,
				cancellationToken).ConfigureAwait(false);

			return ExtPubKey.Parse(response.GetString(2), network);
		}, cancellationToken);

	/// <summary>Shows a receive address on the device screen and returns it; a SLIP-25 path needs the UnlockPath preamble HWI cannot send.</summary>
	public Task<string> ShowAddressAsync(KeyPath fullKeyPath, Network network, CancellationToken cancellationToken) =>
		LockedAsync(async () =>
		{
			bool isCoinJoinAccount = fullKeyPath.IsSlip25KeyPath();
			if (isCoinJoinAccount)
			{
				await CallAsync(TrezorMessages.UnlockPath([Slip25Purpose]), TrezorMessageType.UnlockedPathRequest, cancellationToken).ConfigureAwait(false);
			}

			var response = await CallAsync(
				TrezorMessages.GetAddress(
					fullKeyPath.Indexes,
					GetCoinName(network),
					isCoinJoinAccount ? TrezorInputScriptType.SpendTaproot : TrezorInputScriptType.SpendWitness,
					showDisplay: true),
				TrezorMessageType.Address,
				cancellationToken).ConfigureAwait(false);

			return response.GetString(1);
		}, cancellationToken);

	/// <summary>
	/// Asks the user to authorize coinjoin rounds on the device. The device displays the maximum number of rounds
	/// and the maximum mining fee rate, both confirmed with hold-to-confirm. The authorization is kept in the
	/// device session and one round is spent by each signed coinjoin transaction.
	/// </summary>
	public Task AuthorizeCoinJoinAsync(string coordinatorIdentifier, int maxRounds, FeeRate maxFeeRate, KeyPath accountKeyPath, Network network, CancellationToken cancellationToken) =>
		LockedCallAsync(
			TrezorMessages.AuthorizeCoinJoin(coordinatorIdentifier, (ulong)maxRounds, maxCoordinatorFeeRate: 0, (ulong)maxFeeRate.FeePerK.Satoshi, accountKeyPath.Indexes, GetCoinName(network)),
			TrezorMessageType.Success,
			cancellationToken);

	/// <summary>Gets a SLIP-19 ownership proof for a coin of the authorized coinjoin account, without user interaction.</summary>
	public Task<byte[]> GetOwnershipProofAsync(KeyPath keyPath, byte[] commitmentData, Network network, CancellationToken cancellationToken) =>
		LockedAsync(async () =>
		{
			await CallAsync(TrezorMessages.DoPreauthorized(), TrezorMessageType.PreauthorizedRequest, cancellationToken).ConfigureAwait(false);
			var response = await CallAsync(
				TrezorMessages.GetOwnershipProof(keyPath.Indexes, GetCoinName(network), commitmentData),
				TrezorMessageType.OwnershipProof,
				cancellationToken).ConfigureAwait(false);

			return response.GetBytes(1);
		}, cancellationToken);

	/// <summary>Signs a coinjoin with the standing authorization, without user interaction; returns the 64 byte BIP-340 signatures indexed by input.</summary>
	public Task<Dictionary<int, byte[]>> SignCoinJoinAsync(
		IReadOnlyList<TrezorTxInput> inputs,
		IReadOnlyList<TrezorTxOutput> outputs,
		uint version,
		uint lockTime,
		Money minRegistrableAmount,
		Network network,
		CancellationToken cancellationToken) =>
		LockedAsync(async () =>
		{
			await CallAsync(TrezorMessages.DoPreauthorized(), TrezorMessageType.PreauthorizedRequest, cancellationToken).ConfigureAwait(false);

			// Coordination fees do not exist in the WabiSabi protocol anymore, so the request degenerates to
			// fee_rate = 0 and no_fee_threshold = 0. The coordinator signature is not required by firmware >= 2.7.2.
			var signTx = TrezorMessages.SignTx(
				inputs.Count,
				outputs.Count,
				GetCoinName(network),
				version,
				lockTime,
				coinJoinRequest: (0, 0, (ulong)minRegistrableAmount.Satoshi));

			// Coinjoins are taproot-only, so the device never asks for previous transactions here.
			return await RunSigningFlowAsync(signTx, inputs, outputs, previousTransactions: null, cancellationToken).ConfigureAwait(false);
		}, cancellationToken);

	/// <summary>
	/// Signs a regular transaction on the device, unlocking the SLIP-25 path first when spending from the coinjoin
	/// account; non-taproot inputs need <paramref name="previousTransactions"/> so the device can verify the amounts.
	/// </summary>
	public Task<Dictionary<int, byte[]>> SignTransactionAsync(
		IReadOnlyList<TrezorTxInput> inputs,
		IReadOnlyList<TrezorTxOutput> outputs,
		uint version,
		uint lockTime,
		Network network,
		bool unlockCoinJoinAccount,
		IReadOnlyDictionary<uint256, Transaction>? previousTransactions,
		CancellationToken cancellationToken) =>
		LockedAsync(async () =>
		{
			if (unlockCoinJoinAccount)
			{
				await CallAsync(TrezorMessages.UnlockPath([Slip25Purpose]), TrezorMessageType.UnlockedPathRequest, cancellationToken).ConfigureAwait(false);
			}
			var signTx = TrezorMessages.SignTx(inputs.Count, outputs.Count, GetCoinName(network), version, lockTime, coinJoinRequest: null);
			return await RunSigningFlowAsync(signTx, inputs, outputs, previousTransactions, cancellationToken).ConfigureAwait(false);
		}, cancellationToken);

	private async Task<Dictionary<int, byte[]>> RunSigningFlowAsync(
		TrezorMessage signTx,
		IReadOnlyList<TrezorTxInput> inputs,
		IReadOnlyList<TrezorTxOutput> outputs,
		IReadOnlyDictionary<uint256, Transaction>? previousTransactions,
		CancellationToken cancellationToken)
	{
		Dictionary<int, byte[]> signatures = new();

		var response = await CallRawAsync(signTx, cancellationToken).ConfigureAwait(false);
		while (true)
		{
			if (response.MessageType != TrezorMessageType.TxRequest)
			{
				throw UnexpectedMessage(response, TrezorMessageType.TxRequest);
			}

			var txRequest = TrezorTxRequest.FromMessage(response);
			if (txRequest.SignatureIndex is { } signatureIndex)
			{
				signatures[signatureIndex] = txRequest.Signature;
			}

			// tx_hash names a PREVIOUS transaction, which the device streams through to verify non-taproot input amounts.
			Transaction? previousTransaction = null;
			if (txRequest.TxHash is { } txHash)
			{
				// The device sends the hash in display order (big-endian), like the prev_hash we stream to it.
				var hash = new uint256(txHash, lendian: false);
				if (previousTransactions is null || !previousTransactions.TryGetValue(hash, out previousTransaction))
				{
					throw new TrezorException($"The device asked for unknown previous transaction '{hash}'.");
				}
			}

			switch (txRequest.RequestType)
			{
				case TrezorTxRequestType.TxMeta when previousTransaction is not null:
					response = await CallRawAsync(
						TrezorMessages.TxAckPrevMeta(previousTransaction.Version, previousTransaction.LockTime.Value, previousTransaction.Inputs.Count, previousTransaction.Outputs.Count),
						cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxInput when previousTransaction is not null:
					var prevIn = previousTransaction.Inputs[txRequest.RequestIndex];
					response = await CallRawAsync(
						TrezorMessages.TxAckPrevInput(prevIn.PrevOut.Hash.ToBytes(lendian: false), prevIn.PrevOut.N, prevIn.ScriptSig.ToBytes(), prevIn.Sequence.Value),
						cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxOutput when previousTransaction is not null:
					var prevOut = previousTransaction.Outputs[txRequest.RequestIndex];
					response = await CallRawAsync(
						TrezorMessages.TxAckPrevOutput((ulong)prevOut.Value.Satoshi, prevOut.ScriptPubKey.ToBytes()),
						cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxInput:
					response = await CallRawAsync(inputs[txRequest.RequestIndex].ToTxAckInput(), cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxOutput:
					response = await CallRawAsync(outputs[txRequest.RequestIndex].ToTxAckOutput(), cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxFinished:
					return signatures;

				default:
					throw new TrezorException($"Unexpected transaction data request '{txRequest.RequestType}'.");
			}
		}
	}

	private Task<TrezorMessage> LockedCallAsync(TrezorMessage message, TrezorMessageType expectedResponse, CancellationToken cancellationToken) =>
		LockedAsync(() => CallAsync(message, expectedResponse, cancellationToken), cancellationToken);

	/// <summary>One device conversation at a time: a multi-message operation must not be interleaved with another.</summary>
	private async Task<T> LockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await operation().ConfigureAwait(false);
		}
		finally
		{
			_lock.Release();
		}
	}

	private async Task<TrezorMessage> CallAsync(TrezorMessage message, TrezorMessageType expectedResponse, CancellationToken cancellationToken)
	{
		var response = await CallRawAsync(message, cancellationToken).ConfigureAwait(false);
		if (response.MessageType != expectedResponse)
		{
			throw UnexpectedMessage(response, expectedResponse);
		}
		return response;
	}

	/// <summary>Sends a message and transparently answers the device's button and passphrase requests.</summary>
	private async Task<TrezorMessage> CallRawAsync(TrezorMessage message, CancellationToken cancellationToken)
	{
		var response = await _transport.CallAsync(_bridgeSession, message, cancellationToken).ConfigureAwait(false);
		while (true)
		{
			switch (response.MessageType)
			{
				case TrezorMessageType.ButtonRequest:
					response = await _transport.CallAsync(_bridgeSession, TrezorMessages.ButtonAck(), cancellationToken).ConfigureAwait(false);
					break;

				case TrezorMessageType.PassphraseRequest:
					// Empty passphrase for the standard wallet, like HWI; a hidden wallet's passphrase is typed on
					// the device so it never reaches the host (see FindAsync).
					var passphraseAck = _useOnDevicePassphrase
						? TrezorMessages.PassphraseAckOnDevice()
						: TrezorMessages.PassphraseAck("");
					response = await _transport.CallAsync(_bridgeSession, passphraseAck, cancellationToken).ConfigureAwait(false);
					break;

				case TrezorMessageType.Failure:
					throw new TrezorException($"Trezor failure: {response.GetString(2)}");

				default:
					return response;
			}
		}
	}

	private static TrezorException UnexpectedMessage(TrezorMessage response, TrezorMessageType expected) =>
		new($"Unexpected message '{response.MessageType}' from Trezor, expected '{expected}'.");

	private static string GetCoinName(Network network) =>
		network == Network.Main ? "Bitcoin" : network == Network.TestNet ? "Testnet" : "Regtest";

	/// <summary>SLIP-25 coinjoin account: m/10025'/coin_type'/account'/1' where 1' stands for taproot.</summary>
	public static KeyPath GetCoinJoinAccountKeyPath(Network network) =>
		new(Slip25Purpose, (network == Network.Main ? 0u : 1u) | HardenedIndex, HardenedIndex, 1u | HardenedIndex);

	/// <summary>
	/// Whether the bridge session this device was acquired with still answers; a restarted bridge forgets it
	/// without telling anyone. GetFeatures leaves the device state alone, so a live authorization survives the question.
	/// </summary>
	public async Task<bool> IsSessionAliveAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return false;
		}

		try
		{
			var response = await LockedAsync(() => _transport.CallAsync(_bridgeSession, TrezorMessages.GetFeatures(), cancellationToken), cancellationToken).ConfigureAwait(false);
			return response.MessageType == TrezorMessageType.Features;
		}
		catch (TrezorException)
		{
			return false;
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;

		if (_bridgeSession.Length > 0)
		{
			try
			{
				using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
				_transport.ReleaseAsync(_bridgeSession, cts.Token).GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				Logger.LogDebug($"Failed to release Trezor bridge session: {e.Message}");
			}
		}

		_transport.Dispose();
	}
}
