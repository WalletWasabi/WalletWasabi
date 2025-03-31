using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.Extensions;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public record SigningState : MultipartyTransactionState
{
	public SigningState(RoundParameters parameters, IEnumerable<IEvent> events)
		: base(parameters)
	{
		Events = events.ToImmutableList();
	}

	public ImmutableDictionary<int, WitScript> Witnesses { get; init; } = ImmutableDictionary<int, WitScript>.Empty;
	public bool IsFullySigned => UnpublishedWitnesses.Count + Witnesses.Count == SortedInputs.Count;
	private ImmutableDictionary<int, WitScript> UnpublishedWitnesses { get; init; } = ImmutableDictionary<int, WitScript>.Empty;

	public IEnumerable<Coin> UnsignedInputs => SortedInputs.Where((_, i) => !IsInputSigned(i));

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

	public bool IsInputSigned(int index) => Witnesses.ContainsKey(index) || UnpublishedWitnesses.ContainsKey(index);

	public bool IsInputSigned(OutPoint prevout) => IsInputSigned(GetInputIndex(prevout));

	public int GetInputIndex(OutPoint prevout) => SortedInputs.FindIndex(coin => coin.Outpoint == prevout); // this is inefficient but is only used in tests, see also dotnet/runtime#45366

	public SigningState AddWitness(int index, WitScript witness)
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
	public SigningState PublishWitnesses()
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
