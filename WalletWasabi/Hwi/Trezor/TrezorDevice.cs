using NBitcoin;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// High level operations for Trezor coinjoin support: SLIP-25 account discovery,
/// coinjoin authorization (confirmed on the device with max rounds and max fee),
/// SLIP-19 ownership proofs and transaction signing.
/// These are not available through HWI, so the device is driven directly through the Trezor Bridge.
/// </summary>
public class TrezorDevice : IDisposable
{
	/// <summary>SLIP-25 purpose (10025') dedicated to coinjoin accounts, enforced by the firmware.</summary>
	public const uint Slip25Purpose = 10025 | HardenedIndex;

	private const uint HardenedIndex = 0x80000000;

	/// <summary>First firmware version that accepts coinjoin requests from any coordinator (signature verification against the zkSNACKs key was removed).</summary>
	private static readonly Version MinimumSupportedFirmwareVersion = new(2, 7, 2);

	private TrezorDevice()
	{
		_transport = new TrezorBridgeTransport();
	}

	private readonly TrezorBridgeTransport _transport;
	private readonly SemaphoreSlim _lock = new(1, 1);
	private string _bridgeSession = "";
	private byte[] _deviceSessionId = [];

	public TrezorFeatures? Features { get; private set; }

	/// <summary>Finds and acquires the connected Trezor with the given master fingerprint.</summary>
	public static async Task<TrezorDevice> FindAsync(HDFingerprint? masterFingerprint, CancellationToken cancellationToken)
	{
		IReadOnlyList<TrezorBridgeTransport.BridgeDevice> bridgeDevices;
		using (var enumerationTransport = new TrezorBridgeTransport())
		{
			bridgeDevices = await enumerationTransport.EnumerateAsync(cancellationToken).ConfigureAwait(false);
		}

		if (bridgeDevices.Count == 0)
		{
			throw new TrezorException("No Trezor device found. Connect and unlock the device.");
		}

		foreach (var bridgeDevice in bridgeDevices)
		{
			TrezorDevice? device = null;
			try
			{
#pragma warning disable CA2000 // Dispose objects before losing scope - disposed in the finally block or owned by the caller.
				device = new TrezorDevice();
#pragma warning restore CA2000
				await device.OpenAsync(bridgeDevice, cancellationToken).ConfigureAwait(false);
				if (masterFingerprint is null || await device.GetMasterFingerprintAsync(cancellationToken).ConfigureAwait(false) == masterFingerprint)
				{
					var foundDevice = device;
					device = null;
					return foundDevice;
				}
			}
			catch (TrezorException e)
			{
				Logger.LogDebug($"Skipping Trezor device '{bridgeDevice.Path}': {e.Message}");
			}
			finally
			{
				device?.Dispose();
			}
		}

		throw new TrezorException($"No Trezor device with master fingerprint '{masterFingerprint}' found.");
	}

	private async Task OpenAsync(TrezorBridgeTransport.BridgeDevice bridgeDevice, CancellationToken cancellationToken)
	{
		_bridgeSession = await _transport.AcquireAsync(bridgeDevice, cancellationToken).ConfigureAwait(false);

		var features = await CallAsync(TrezorMessages.Initialize(), TrezorMessageType.Features, cancellationToken).ConfigureAwait(false);
		Features = TrezorFeatures.FromMessage(features);
		_deviceSessionId = features.GetBytes(35);

		if (Features.Model == "1")
		{
			throw new TrezorException("Trezor Model One does not support coinjoin. A Trezor Model T or newer is required.");
		}
		if (Features.Version < MinimumSupportedFirmwareVersion)
		{
			throw new TrezorException($"Trezor firmware {Features.Version} is too old for coinjoin with this coordinator. Version {MinimumSupportedFirmwareVersion} or newer is required.");
		}
	}

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

	/// <summary>Gets the xpub of the SLIP-25 coinjoin account. The device shows a confirmation for unlocking the coinjoin path.</summary>
	public async Task<ExtPubKey> GetCoinJoinXpubAsync(KeyPath accountKeyPath, Network network, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await CallAsync(TrezorMessages.UnlockPath([Slip25Purpose]), TrezorMessageType.UnlockedPathRequest, cancellationToken).ConfigureAwait(false);
			var response = await CallAsync(
				TrezorMessages.GetPublicKey(accountKeyPath.Indexes, GetCoinName(network), TrezorInputScriptType.SpendTaproot),
				TrezorMessageType.PublicKey,
				cancellationToken).ConfigureAwait(false);

			return ExtPubKey.Parse(response.GetString(2), network);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Asks the user to authorize coinjoin rounds on the device. The device displays the maximum number of rounds
	/// and the maximum mining fee rate, both confirmed with hold-to-confirm. The authorization is kept in the
	/// device session and one round is spent by each signed coinjoin transaction.
	/// </summary>
	public async Task AuthorizeCoinJoinAsync(string coordinatorIdentifier, int maxRounds, FeeRate maxFeeRate, KeyPath accountKeyPath, Network network, CancellationToken cancellationToken)
	{
		ulong maxFeePerKvbyte = (ulong)maxFeeRate.FeePerK.Satoshi;
		await LockedCallAsync(
			TrezorMessages.AuthorizeCoinJoin(coordinatorIdentifier, (ulong)maxRounds, maxCoordinatorFeeRate: 0, maxFeePerKvbyte, accountKeyPath.Indexes, GetCoinName(network)),
			TrezorMessageType.Success,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Gets a SLIP-19 ownership proof for a coin of the authorized coinjoin account, without user interaction.</summary>
	public async Task<byte[]> GetOwnershipProofAsync(KeyPath keyPath, byte[] commitmentData, Network network, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await CallAsync(TrezorMessages.DoPreauthorized(), TrezorMessageType.PreauthorizedRequest, cancellationToken).ConfigureAwait(false);
			var response = await CallAsync(
				TrezorMessages.GetOwnershipProof(keyPath.Indexes, GetCoinName(network), commitmentData),
				TrezorMessageType.OwnershipProof,
				cancellationToken).ConfigureAwait(false);

			return response.GetBytes(1);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Signs a coinjoin transaction with the previously given authorization, without user interaction.
	/// Returns the 64 byte BIP-340 signatures indexed by input.
	/// </summary>
	public async Task<Dictionary<int, byte[]>> SignCoinJoinAsync(
		IReadOnlyList<TrezorTxInput> inputs,
		IReadOnlyList<TrezorTxOutput> outputs,
		uint version,
		uint lockTime,
		Money minRegistrableAmount,
		Network network,
		CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
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

			return await RunSigningFlowAsync(signTx, inputs, outputs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Signs a regular transaction spending SLIP-25 coinjoin account coins, for example to move mixed funds
	/// out of the coinjoin account. The user confirms every output on the device.
	/// </summary>
	public async Task<Dictionary<int, byte[]>> SignTransactionAsync(
		IReadOnlyList<TrezorTxInput> inputs,
		IReadOnlyList<TrezorTxOutput> outputs,
		uint version,
		uint lockTime,
		Network network,
		CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await CallAsync(TrezorMessages.UnlockPath([Slip25Purpose]), TrezorMessageType.UnlockedPathRequest, cancellationToken).ConfigureAwait(false);
			var signTx = TrezorMessages.SignTx(inputs.Count, outputs.Count, GetCoinName(network), version, lockTime, coinJoinRequest: null);
			return await RunSigningFlowAsync(signTx, inputs, outputs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_lock.Release();
		}
	}

	private async Task<Dictionary<int, byte[]>> RunSigningFlowAsync(
		TrezorMessage signTx,
		IReadOnlyList<TrezorTxInput> inputs,
		IReadOnlyList<TrezorTxOutput> outputs,
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

			switch (txRequest.RequestType)
			{
				case TrezorTxRequestType.TxInput:
					response = await CallRawAsync(inputs[txRequest.RequestIndex].ToTxAckInput(), cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxOutput:
					response = await CallRawAsync(outputs[txRequest.RequestIndex].ToTxAckOutput(), cancellationToken).ConfigureAwait(false);
					break;

				case TrezorTxRequestType.TxFinished:
					return signatures;

				default:
					throw new TrezorException($"Unexpected transaction data request '{txRequest.RequestType}'. Only taproot inputs are expected in a coinjoin.");
			}
		}
	}

	private async Task<TrezorMessage> LockedCallAsync(TrezorMessage message, TrezorMessageType expectedResponse, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await CallAsync(message, expectedResponse, cancellationToken).ConfigureAwait(false);
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
					// The standard (empty passphrase) wallet is used, same as Wasabi's HWI based signing.
					response = await _transport.CallAsync(_bridgeSession, TrezorMessages.PassphraseAck(""), cancellationToken).ConfigureAwait(false);
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

	public void Dispose()
	{
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
