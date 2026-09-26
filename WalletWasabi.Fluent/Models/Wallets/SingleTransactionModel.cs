using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Models.Wallets;

public abstract class SingleTransactionModel : TransactionModel
{
	public required uint Confirmations { get; init; }

	public required Func<string> HexFunction { get; init; }
	public Lazy<string> Hex => new(HexFunction());

	public required Func<IReadOnlyCollection<OutPoint>> ForeignInputsFunction { get; init; }
	public Lazy<IReadOnlyCollection<OutPoint>> ForeignInputs => new(ForeignInputsFunction());
	public required IReadOnlyCollection<SmartCoin> WalletInputs { get; init; }

	public required Func<IReadOnlyCollection<IndexedTxOut>> ForeignOutputsFunction { get; init; }
	public Lazy<IReadOnlyCollection<IndexedTxOut>> ForeignOutputs => new(ForeignOutputsFunction());
	public required IReadOnlyCollection<SmartCoin> WalletOutputs { get; init; }

	public FeeRate? FeeRate { get; init; }
}
