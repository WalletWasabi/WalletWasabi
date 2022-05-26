using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public record SigningState : MultipartyTransactionState
{
	public SigningState(RoundParameters parameters, IEnumerable<IEvent> events)
		: base(parameters)
	{
		Events = events.ToImmutableList();
	}

	public ImmutableDictionary<int, WitScript> Witnesses { get; init; } = ImmutableDictionary<int, WitScript>.Empty;

	public bool IsFullySigned => Witnesses.Count == SortedInputs.Count;

	[JsonIgnore]
	public IEnumerable<Coin> UnsignedInputs => SortedInputs.Where((_, i) => !IsInputSigned(i));

	[JsonIgnore]
	public List<Coin> SortedInputs => Inputs
			.OrderByDescending(x => x.Amount)
			.ThenBy(x => x.Outpoint.ToBytes(), ByteArrayComparer.Comparer)
			.ToList();

	[JsonIgnore]
	public List<TxOut> SortedOutputs => Outputs
			.GroupBy(x => x.ScriptPubKey)
			.Select(x => new TxOut(x.Sum(y => y.Value), x.Key))
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.ScriptPubKey.ToBytes(true), ByteArrayComparer.Comparer)
			.ToList();

	public bool IsInputSigned(int index) => Witnesses.ContainsKey(index);

	public bool IsInputSigned(OutPoint prevout) => IsInputSigned(GetInputIndex(prevout));

	public int GetInputIndex(OutPoint prevout) => SortedInputs.FindIndex(coin => coin.Outpoint == prevout); // this is inefficient but is only used in tests, see also dotnet/runtime#45366

	public SigningState AddWitness(int index, WitScript witness)
	{
		if (IsInputSigned(index))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WitnessAlreadyProvided);
		}

		// Verify witness.
		// 1. Copy UnsignedCoinJoin.
		Transaction cjCopy = CreateUnsignedTransaction();

		// 2. Sign the copy.
		cjCopy.Inputs[index].WitScript = witness;

		// 3. Convert the current input to IndexedTxIn.
		IndexedTxIn currentIndexedInput = cjCopy.Inputs.AsIndexedInputs().Skip(index).First();

		// 4. Find the corresponding registered input.
		Coin registeredCoin = SortedInputs[index];

		// 5. Verify if currentIndexedInput is correctly signed, if not, return the specific error.
		if (!currentIndexedInput.VerifyScript(registeredCoin, out ScriptError error))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongCoinjoinSignature); // TODO keep script error
		}

		return this with { Witnesses = Witnesses.Add(index, witness) };
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

		foreach (var (index, witness) in Witnesses)
		{
			tx.Inputs[index].WitScript = witness;
		}

		return tx;
	}
}
