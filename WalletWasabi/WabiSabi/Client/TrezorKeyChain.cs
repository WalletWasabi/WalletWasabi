using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Hwi.Trezor;

namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// Key chain backed by a Trezor device acting as a remote signer for coinjoin rounds.
/// Requires a coinjoin authorization on the device (see <see cref="TrezorDevice.AuthorizeCoinJoinAsync"/>),
/// after which ownership proofs and signatures are produced without user interaction.
/// </summary>
public class TrezorKeyChain : IKeyChain, IDisposable
{
	/// <summary>Firmware clamps the minimum registrable output amount to at most this value anyway (MIN_REGISTRABLE_OUTPUT_AMOUNT).</summary>
	private static readonly Money MinRegistrableAmount = Money.Satoshis(5000);

	public TrezorKeyChain(TrezorDevice device, KeyManager keyManager)
	{
		if (!keyManager.IsHardwareWallet)
		{
			throw new ArgumentException("A Trezor key chain requires a hardware wallet key manager.");
		}

		_device = device;
		_keyManager = keyManager;
	}

	private readonly TrezorDevice _device;
	private readonly KeyManager _keyManager;
	private readonly object _signingLock = new();
	private (uint256 TxId, Dictionary<OutPoint, WitScript> Witnesses)? _signedTransactionCache;

	public TrezorDevice Device => _device;

	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData commitmentData)
	{
		var keyPath = GetKeyPath(destination.ScriptPubKey);
		byte[] proof = _device
			.GetOwnershipProofAsync(keyPath, commitmentData.ToBytes(), _keyManager.GetNetwork(), CancellationToken.None)
			.GetAwaiter()
			.GetResult();

		return OwnershipProof.FromBytes(proof);
	}

	/// <summary>
	/// The device signs all our inputs of the coinjoin in a single preauthorized SignTx call, because every call
	/// spends one authorized round. The witnesses are cached, so the per-coin calls of the signing phase
	/// hit the device only once per round.
	/// </summary>
	public Transaction Sign(Transaction transaction, Coin coin, PrecomputedTransactionData precomputedTransactionData)
	{
		lock (_signingLock)
		{
			if (_signedTransactionCache is not { } cache || cache.TxId != transaction.GetHash())
			{
				var spentOutputs = ((TaprootReadyPrecomputedTransactionData)precomputedTransactionData).SpentOutputs;
				cache = (transaction.GetHash(), SignOnDevice(transaction, spentOutputs));
				_signedTransactionCache = cache;
			}

			transaction = transaction.Clone();
			var txInput = transaction.Inputs.AsIndexedInputs().FirstOrDefault(input => input.PrevOut == coin.Outpoint)
				?? throw new InvalidOperationException("Missing input.");
			txInput.WitScript = cache.Witnesses[coin.Outpoint];
			return transaction;
		}
	}

	private Dictionary<OutPoint, WitScript> SignOnDevice(Transaction transaction, TxOut[] spentOutputs)
	{
		var network = _keyManager.GetNetwork();

		var inputs = transaction.Inputs.AsIndexedInputs()
			.Select(input =>
			{
				var spentOutput = spentOutputs[input.Index];
				var keyPath = _keyManager.TryGetKeyPath(spentOutput.ScriptPubKey);
				return new TrezorTxInput
				{
					AddressN = keyPath?.Indexes ?? [],
					PrevHash = input.PrevOut.Hash.ToBytes(lendian: false),
					PrevIndex = input.PrevOut.N,
					Sequence = input.TxIn.Sequence.Value,
					ScriptType = keyPath is null ? TrezorInputScriptType.External : TrezorInputScriptType.SpendTaproot,
					Amount = (ulong)spentOutput.Value.Satoshi,
					ScriptPubKey = spentOutput.ScriptPubKey.ToBytes(),
				};
			})
			.ToList();

		var outputs = transaction.Outputs
			.Select(output =>
			{
				// Only SLIP-25 outputs are ours from the device's point of view, everything else is foreign.
				var keyPath = _keyManager.TryGetKeyPath(output.ScriptPubKey);
				bool isOurs = keyPath?.IsSlip25KeyPath() is true;
				return new TrezorTxOutput
				{
					AddressN = isOurs ? keyPath!.Indexes : [],
					Address = isOurs ? "" : output.ScriptPubKey.GetDestinationAddress(network)!.ToString(),
					Amount = (ulong)output.Value.Satoshi,
					ScriptType = isOurs ? TrezorOutputScriptType.PayToTaproot : TrezorOutputScriptType.PayToAddress,
				};
			})
			.ToList();

		var signatures = _device
			.SignCoinJoinAsync(inputs, outputs, (uint)transaction.Version, transaction.LockTime.Value, MinRegistrableAmount, network, CancellationToken.None)
			.GetAwaiter()
			.GetResult();

		return signatures.ToDictionary(
			signature => transaction.Inputs[signature.Key].PrevOut,
			signature => new WitScript(Op.GetPushOp(signature.Value)));
	}

	private KeyPath GetKeyPath(Script scriptPubKey) =>
		_keyManager.TryGetKeyPath(scriptPubKey)
			?? throw new InvalidOperationException($"The key path for '{scriptPubKey}' was not found.");

	public void Dispose()
	{
		_device.Dispose();
	}
}
