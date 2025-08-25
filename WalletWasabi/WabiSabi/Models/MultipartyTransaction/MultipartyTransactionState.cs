using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public interface IEvent;

public record RoundCreated(RoundParameters RoundParameters) : IEvent;
public record InputAdded(Coin Coin, OwnershipProof OwnershipProof) : IEvent;
public record OutputAdded(TxOut Output) : IEvent;

public record MultipartyTransactionState
{
	public MultipartyTransactionState(RoundParameters parameters, ImmutableList<IEvent>? events = null)
	{
		var builder = ImmutableList.CreateBuilder<IEvent>();
		builder.Add(new RoundCreated(parameters));
		Events = events ?? builder.ToImmutable();
	}

	public RoundParameters Parameters => Events.OfType<RoundCreated>().Single().RoundParameters;

	public IEnumerable<Coin> Inputs => Events.OfType<InputAdded>().Select(x => x.Coin);
	public IEnumerable<TxOut> Outputs => Events.OfType<OutputAdded>().Select(x => x.Output);

	public Money Balance => Inputs.Sum(x => x.Amount) - Outputs.Sum(x => x.Value);
	public int EstimatedInputsVsize => Inputs.Sum(x => x.TxOut.ScriptPubKey.EstimateInputVsize());
	public int OutputsVsize => Outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());

	public int EstimatedVsize => MultipartyTransactionParameters.SharedOverhead + EstimatedInputsVsize + OutputsVsize;

	public int UnpaidSharedOverhead { get; init; } = MultipartyTransactionParameters.SharedOverhead;

	public FeeRate EffectiveFeeRate => new(Balance, EstimatedVsize - UnpaidSharedOverhead);

	public ImmutableList<IEvent> Events { get; init; } = ImmutableList<IEvent>.Empty;
	public ImmutableDictionary<int, WitScript> Witnesses { get; init; } = ImmutableDictionary<int, WitScript>.Empty;
	public bool IsFullySigned => UnpublishedWitnesses.Count + Witnesses.Count == SortedInputs.Count && SortedInputs.Count > 0;
	private ImmutableDictionary<int, WitScript> UnpublishedWitnesses { get; init; } = ImmutableDictionary<int, WitScript>.Empty;
	public IEnumerable<Coin> UnsignedInputs => Enumerable.Where<Coin>(SortedInputs, (_, i) => !IsInputSigned(i));

	public List<Coin> SortedInputs => Inputs
		.OrderByDescending(x => x.Amount)
		.ThenBy(x => x.Outpoint.ToBytes(), ByteArrayComparer.Comparer)
		.ToList();

	public List<TxOut> SortedOutputs => Outputs
		.GroupBy(x => x.ScriptPubKey)
		.Select(x => new TxOut(x.Sum(y => y.Value), x.Key))
		.OrderByDescending(x => x.Value)
		.ThenBy(x => x.ScriptPubKey.ToBytes(true), ByteArrayComparer.Comparer)
		.ToList();

	public MultipartyTransactionState GetStateFrom(int stateId) =>
		this with
		{
			Events = Events.Skip(stateId).ToImmutableList()
		};

	public MultipartyTransactionState Merge(MultipartyTransactionState diff) =>
		this with
		{
			Events = Events.AddRange(diff.Events)
		};

	public MultipartyTransactionState AddPreviousStates(MultipartyTransactionState origin, uint256 roundId)
	{
		VerifyOwnershipProofs(origin, Events, roundId);
		return this with
		{
			Events = origin.Events.AddRange(Events)
		};
	}

	private void VerifyOwnershipProofs(MultipartyTransactionState state, ImmutableList<IEvent> events, uint256 roundId)
	{
		var coinJoinInputCommitData = new CoinJoinInputCommitmentData(state.Parameters.CoordinationIdentifier, roundId);
		var anyInvalidInput =
			events.OfType<InputAdded>().Any(x => !OwnershipProof.VerifyCoinJoinInputProof(x.OwnershipProof, x.Coin.ScriptPubKey, coinJoinInputCommitData));

		if (anyInvalidInput)
		{
			throw new InvalidOperationException(
				"The coordinator is cheating by adding inputs to rounds that were created to be registered in different rounds.");
		}
	}

	public MultipartyTransactionState AddInput(Coin coin, OwnershipProof ownershipProof, CoinJoinInputCommitmentData coinJoinInputCommitmentData)
	{
		var prevout = coin.TxOut;

		if (!OwnershipProof.VerifyCoinJoinInputProof(ownershipProof, coin.TxOut.ScriptPubKey, coinJoinInputCommitmentData))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongOwnershipProof);
		}

		if (!StandardScripts.IsStandardScriptPubKey(prevout.ScriptPubKey))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonStandardInput);
		}

		if (!Parameters.AllowedInputTypes.Any(x => prevout.ScriptPubKey.IsScriptType(x)))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
		}

		if (prevout.Value < Parameters.AllowedInputAmounts.Min)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
		}

		if (prevout.Value > Parameters.AllowedInputAmounts.Max)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
		}

		if (prevout.Value <= Parameters.MiningFeeRate.GetFee(prevout.ScriptPubKey.EstimateInputVsize()))
		{
			// Inputs must contribute more than they cost to spend because:
			// - Such inputs contribute nothing to privacy and may degrade it
			//   because they must be paid for, when constraining sub-transactions
			//   this may be useful for disambiguating between different input
			//   clusters in the same way that the existence of a dust output
			//   might.
			// - Space in standard transaction is limited and must be shared
			//   between participants.
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.UneconomicalInput);
		}

		if (Inputs.Any(x => x.Outpoint == coin.Outpoint))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonUniqueInputs);
		}

		return this with { Events = Events.Add(new InputAdded(coin, ownershipProof)) };
	}

	public MultipartyTransactionState AddOutput(TxOut output)
	{
		if (output.Value < Parameters.AllowedOutputAmounts.Min)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
		}

		return AddOutputNoMinAmountCheck(output);
	}

	public MultipartyTransactionState AddOutputNoMinAmountCheck(TxOut output)
	{
		if (output.Value > Parameters.AllowedOutputAmounts.Max)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
		}

		if (output.IsDust())
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.DustOutput);
		}

		if (!StandardScripts.IsStandardScriptPubKey(output.ScriptPubKey))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonStandardOutput);
		}

		// Only one OP_RETURN is allowed per standard transaction, but this
		// check is not implemented since there is no OP_RETURN ScriptType,
		// which means no OP_RETURN outputs can be registered at all.
		if (!Parameters.AllowedOutputTypes.Any(x => output.ScriptPubKey.IsScriptType(x)))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
		}

		return this with { Events = Events.Add(new OutputAdded(output)) };
	}

	public MultipartyTransactionState Finalize()
	{
		if (EstimatedVsize > Parameters.MaxTransactionSize)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.SizeLimitExceeded, $"Transaction size is {EstimatedVsize} bytes, which exceeds the limit of {Parameters.MaxTransactionSize} bytes.");
		}

		if (1.001m * EffectiveFeeRate.SatoshiPerByte < Parameters.MiningFeeRate.SatoshiPerByte)
		{
			var tx = CreateUnsignedTransaction();
			var txHex = tx.ToHex();

			throw new WabiSabiProtocolException(
				WabiSabiProtocolErrorCode.InsufficientFees,
				$"Effective fee rate {EffectiveFeeRate} is less than required {Parameters.MiningFeeRate}. RawTx: {txHex}");
		}

		return this;
	}

	public MultipartyTransactionState AsPayingForSharedOverhead() =>
		this with
		{
			UnpaidSharedOverhead = 0
		};

	public bool IsInputSigned(int index) => Witnesses.ContainsKey(index) || UnpublishedWitnesses.ContainsKey(index);
	public bool IsInputSigned(OutPoint prevout) => IsInputSigned(GetInputIndex(prevout));
	public int GetInputIndex(OutPoint prevout) => SortedInputs.FindIndex(coin => coin.Outpoint == prevout); // this is inefficient but is only used in tests, see also dotnet/runtime#45366

	public MultipartyTransactionState AddWitness(int index, WitScript witness)
	{
		if (IsInputSigned(index))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WitnessAlreadyProvided);
		}

		// Verify witness.
		// 1. Find the corresponding registered input.
		Coin registeredCoin = SortedInputs[index];

		// 2. Check the witness is not too long.
		if (VirtualSizeHelpers.VirtualSize(Constants.InputBaseSizeInBytes, witness.ToBytes().Length) > registeredCoin.ScriptPubKey.EstimateInputVsize())
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.SignatureTooLong);
		}

		// 3. Copy UnsignedCoinJoin.
		Transaction cjCopy = CreateUnsignedTransaction();

		// 4. Sign the copy.
		cjCopy.Inputs[index].WitScript = witness;

		// 5. Convert the current input to IndexedTxIn.
		IndexedTxIn currentIndexedInput = cjCopy.Inputs.AsIndexedInputs().Skip(index).First();

		// 6. Verify if currentIndexedInput is correctly signed, if not, return the specific error.
		var precomputedTransactionData = cjCopy.PrecomputeTransactionData(SortedInputs.Select(x => x.TxOut).ToArray());
		if (!currentIndexedInput.VerifyScript(registeredCoin, ScriptVerify.Standard, precomputedTransactionData, out ScriptError error))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongCoinjoinSignature); // TODO keep script error
		}

		return this with { UnpublishedWitnesses = UnpublishedWitnesses.Add(index, witness) };
	}

	public MultipartyTransactionState PublishWitnesses()
	{
		return this with
		{
			Witnesses = Witnesses.AddRange(UnpublishedWitnesses),
			UnpublishedWitnesses = ImmutableDictionary<int, WitScript>.Empty
		};
	}

	public Transaction CreateUnsignedTransaction()
	{
		var tx = Parameters.CreateTransaction();

		foreach (var coin in SortedInputs)
		{
			// implied:
			// nSequence = FINAL
			tx.Inputs.Add(coin.Outpoint);
		}

		foreach (var txout in SortedOutputs)
		{
			tx.Outputs.Add(txout);
		}

		return tx;
	}

	public Transaction CreateTransaction()
	{
		var tx = CreateUnsignedTransaction();

		foreach (var (index, witness) in Witnesses.Concat(UnpublishedWitnesses))
		{
			tx.Inputs[index].WitScript = witness;
		}

		return tx;
	}

	public TransactionWithPrecomputedData CreateUnsignedTransactionWithPrecomputedData()
	{
		var tx = CreateUnsignedTransaction();
		var precomputeTransactionData = tx.PrecomputeTransactionData(Inputs.ToArray());
		return new TransactionWithPrecomputedData(tx, precomputeTransactionData);
	}
}

public record TransactionWithPrecomputedData(
	Transaction Transaction,
	PrecomputedTransactionData PrecomputedTransactionData);
