using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using static WalletWasabi.Tests.Helpers.BitcoinFactory;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

public static class TransactionBuilder
{
	public static IEnumerable<TxIn> Inputs(int count) => Enumerable
		.Range(0, count)
		.Select(_ => new TxIn(CreateOutPoint()));

	public static IEnumerable<TxOut> Outputs(params Money[] amounts) => amounts
		.Select(amount => new TxOut(amount, new Key().GetScriptPubKey(ScriptPubKeyType.Segwit)));

	public static IEnumerable<TxOut> Outputs(int count) => Outputs(Enumerable
		.Repeat(Money.Coins(1m), count).ToArray());

	public static Func<KeyManager, IEnumerable<SmartCoin>> InputCoins(params Money[] amounts) =>
		km => amounts.Select(amount => CreateSmartCoin(CreateHdPubKey(km), amount));

	public static Func<KeyManager, IEnumerable<SmartCoin>> InputCoins(int count) =>
		InputCoins(Enumerable.Repeat(Money.Coins(1m), count).ToArray());

	public static Func<KeyManager, IEnumerable<(HdPubKey PubKey, Money Amount)>> OutputCoins(params Money[] amounts) =>
		km => amounts.Select(amount => (CreateHdPubKey(km), amount));

	public static Func<KeyManager, IEnumerable<(HdPubKey PubKey, Money Amount)>> OutputCoins(params (HdPubKey PubKey, Money Amount)[] coins) =>
		_ => coins;

	public static Func<KeyManager, IEnumerable<(HdPubKey pubKey, Money Amount)>> OutputCoins(int count) =>
		km => Enumerable.Range(0, count).Select(x => (CreateHdPubKey(km), Money.Coins(1m)));

	public static SmartTransaction CreateTransaction(int foreignInputCount, int foreignOutputCount, int walletInputCount, int walletOutputCount) =>
		CreateTransaction(
			Inputs(foreignInputCount),
			Outputs(foreignOutputCount),
			InputCoins(walletInputCount),
			OutputCoins(walletOutputCount));

	public static SmartTransaction CreateTransaction(
		IEnumerable<TxIn> foreignInputs,
		IEnumerable<TxOut> foreignOutputs,
		Func<KeyManager, IEnumerable<SmartCoin>> walletInputFactory,
		Func<KeyManager, IEnumerable<(HdPubKey PubKey, Money Amount)>> walletOutputFactory)
	{
		var km = ServiceFactory.CreateKeyManager();
		return CreateTransaction(foreignInputs, foreignOutputs, walletInputFactory(km), walletOutputFactory(km));
	}

	public static SmartTransaction CreateTransaction(
		IEnumerable<TxIn> foreignInputs,
		IEnumerable<TxOut> foreignOutputs,
		IEnumerable<SmartCoin> walletInputs,
		IEnumerable<(HdPubKey PubKey, Money Amount)> walletOutputs)
	{
		var walletInputArray = walletInputs.ToArray();
		var walletOutputArray = walletOutputs.ToArray();
		var foreignOutputArray = foreignOutputs.ToArray();

		var stx = CreateTransaction(
			foreignInputs.Concat(walletInputArray.Select(x => new TxIn(x.Outpoint))),
			foreignOutputArray.Concat(walletOutputArray.Select(x => new TxOut(x.Amount, x.PubKey.P2wpkhScript))));

		foreach (var input in walletInputArray)
		{
			input.SpenderTransaction = stx;
			stx.TryAddWalletInput(input);
		}

		foreach (var (pk, i) in walletOutputArray.Select((x, i) => (x.PubKey, i + foreignOutputArray.Length)))
		{
			stx.TryAddWalletOutput(new SmartCoin(stx, (uint) i, pk));
		}

		return stx;
	}

	public static SmartTransaction CreateTransaction(IEnumerable<TxIn> inputs, IEnumerable<TxOut> outputs)
	{
		var tx = Transaction.Create(Network.Main);
		tx.Inputs.AddRange(inputs);
		tx.Outputs.AddRange(outputs);

		return new SmartTransaction(tx, Height.Mempool);
	}
}
