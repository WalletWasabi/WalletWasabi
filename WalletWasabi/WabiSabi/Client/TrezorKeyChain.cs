using WalletWasabi.Hwi.Trezor;

namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// Key chain backed by a Trezor device acting as a remote signer for coinjoin rounds.
/// Requires a coinjoin authorization on the device (see <see cref="AuthorizeAsync"/>),
/// after which ownership proofs and signatures are produced without user interaction.
/// </summary>
public class TrezorKeyChain : IKeyChain, IDisposable
{
	/// <summary>Firmware clamps the minimum registrable output amount to at most this value anyway (MIN_REGISTRABLE_OUTPUT_AMOUNT).</summary>
	private static readonly Money MinRegistrableAmount = Money.Satoshis(5000);

	public TrezorKeyChain(TrezorDevice device, KeyManager keyManager)
	{
		Device = device;
		_keyManager = keyManager;
	}

	private readonly KeyManager _keyManager;
	private readonly object _signingLock = new();
	private (uint256 TxId, Dictionary<OutPoint, WitScript> Witnesses)? _signedTransactionCache;

	// The commitment data of the round being mixed. A wallet takes part in one round at a time,
	// so the commitment seen at input registration is the one to use at signing.
	private volatile byte[] _roundCommitmentData = [];

	public TrezorDevice Device { get; }

	/// <summary>Far longer than any round phase lasts, so only a hung bridge or device runs into it, and its round is lost by then anyway.</summary>
	internal TimeSpan DeviceCallTimeout { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary>The cap the device was authorized with; set by the authorization, so it never drifts from what the device enforces.</summary>
	public FeeRate? MaxMiningFeeRate { get; private set; }

	/// <summary>Set when the device fails a preauthorized call, cleared by the next authorization.</summary>
	public bool NeedsAuthorization { get; private set; }

	/// <summary>The device has to be asked, and it verifies every foreign input of the round before it signs.</summary>
	public bool SigningTakesTime => true;

	/// <summary>Has the device authorize the wallet's coinjoin limits (shown on its screen, confirmed physically); the rounds are then signed without interaction.</summary>
	public async Task AuthorizeAsync(string coordinatorIdentifier, CancellationToken cancellationToken)
	{
		var maxMiningFeeRate = new FeeRate(_keyManager.CoinJoinDeviceMaxMiningFeeRate);
		await Device
			.AuthorizeCoinJoinAsync(coordinatorIdentifier, _keyManager.CoinJoinDeviceMaxRounds, maxMiningFeeRate, _keyManager.TaprootAccountKeyPath, _keyManager.GetNetwork(), cancellationToken)
			.ConfigureAwait(false);

		MaxMiningFeeRate = maxMiningFeeRate;
		NeedsAuthorization = false;
	}

	// IKeyChain is a synchronous interface (soft wallets sign in-process), while the device i/o is async.
	// Blocking here is safe: every task below runs with ConfigureAwait(false) end to end, and coinjoin
	// signing happens on thread pool threads without a synchronization context to deadlock on.
	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData commitmentData)
	{
		var keyPath = _keyManager.TryGetKeyPath(destination.ScriptPubKey)
			?? throw new InvalidOperationException($"The key path for '{destination.ScriptPubKey}' was not found.");
		_roundCommitmentData = commitmentData.ToBytes();
		byte[] proof = WhileAuthorized(cancel => Device.GetOwnershipProofAsync(keyPath, _roundCommitmentData, _keyManager.GetNetwork(), cancel));

		return OwnershipProof.FromBytes(proof);
	}

	/// <summary>
	/// The device signs all our inputs of the coinjoin in a single preauthorized SignTx call, because every call
	/// spends one authorized round. The witnesses are cached, so the per-coin calls of the signing phase
	/// hit the device only once per round.
	/// </summary>
	public Transaction Sign(TransactionWithPrecomputedData unsignedCoinJoin, Coin coin)
	{
		lock (_signingLock)
		{
			var transaction = unsignedCoinJoin.Transaction;
			if (_signedTransactionCache is not { } cache || cache.TxId != transaction.GetHash())
			{
				cache = (transaction.GetHash(), SignOnDevice(unsignedCoinJoin));
				_signedTransactionCache = cache;
			}

			transaction = transaction.Clone();
			var txInput = transaction.Inputs.AsIndexedInputs().FirstOrDefault(input => input.PrevOut == coin.Outpoint)
				?? throw new InvalidOperationException("Missing input.");
			txInput.WitScript = cache.Witnesses[coin.Outpoint];
			return transaction;
		}
	}

	private Dictionary<OutPoint, WitScript> SignOnDevice(TransactionWithPrecomputedData unsignedCoinJoin)
	{
		var network = _keyManager.GetNetwork();
		var transaction = unsignedCoinJoin.Transaction;
		var spentOutputs = ((TaprootReadyPrecomputedTransactionData)unsignedCoinJoin.PrecomputedTransactionData).SpentOutputs;

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
					OwnershipProof = keyPath is null ? ForeignProof(input.PrevOut) : [],
					CommitmentData = keyPath is null ? _roundCommitmentData : [],
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

		var signatures = WhileAuthorized(cancel => Device.SignCoinJoinAsync(inputs, outputs, transaction.Version, transaction.LockTime.Value, MinRegistrableAmount, network, cancel));

		return signatures.ToDictionary(
			signature => transaction.Inputs[signature.Key].PrevOut,
			signature => new WitScript(Op.GetPushOp(signature.Value)));

		byte[] ForeignProof(OutPoint outpoint) =>
			unsignedCoinJoin.OwnershipProofs.TryGetValue(outpoint, out var proof)
				? proof.ToBytes()
				: throw new InvalidOperationException($"The ownership proof of the foreign input '{outpoint}' was not found.");
	}

	/// <summary>
	/// A preauthorized call fails once the authorization is spent or the session is gone, and it will keep failing until the device
	/// authorizes again. A call that does not come back in time is cut off, so the round moves on, and it fails the same way: the
	/// session it leaves behind cannot be trusted.
	/// </summary>
	private T WhileAuthorized<T>(Func<CancellationToken, Task<T>> deviceCall)
	{
		using var timeout = new CancellationTokenSource(DeviceCallTimeout);
		try
		{
			return deviceCall(timeout.Token).GetAwaiter().GetResult();
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			NeedsAuthorization = true;
			throw new TrezorException($"The device did not answer within {DeviceCallTimeout.TotalMinutes} minutes.");
		}
		catch (TrezorException)
		{
			NeedsAuthorization = true;
			throw;
		}
	}

	public void Dispose()
	{
		Device.Dispose();
	}
}
