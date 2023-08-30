using NBitcoin;
using NBitcoin.DataEncoders;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Extensions;

namespace WalletWasabi.Tests.UnitTests;

public class TestWallet
{
	public TestWallet(string name, RPCClient rpc)
	{
		Rpc = rpc;
		ExtKey = new ExtKey(Encoders.Hex.EncodeData(Encoding.UTF8.GetBytes(name)));
	}
	public TestWallet(string name, Mnemonic mnemonic, RPCClient rpc)
	{
		Rpc = rpc;
		ExtKey = mnemonic.DeriveExtKey();
	}

	private RPCClient Rpc { get; }
	public List<Coin> Utxos { get; } = new();
	private ExtKey ExtKey { get; }
	public Dictionary<Script, ExtKey> ScriptPubKeys { get; } = new();
	private uint NextKeyIndex { get; set; } = 0;

	public async Task GenerateAsync(int blocks, CancellationToken cancellationToken)
	{
		var miningAddress = CreateNewAddress();
		var blockIds = await Rpc.GenerateToAddressAsync(blocks, miningAddress, cancellationToken).ConfigureAwait(false);
		foreach (var blockId in blockIds)
		{
			var block = await Rpc.GetBlockAsync(blockId, cancellationToken).ConfigureAwait(false);
			var coinbaseTx = block.Transactions[0];
			ScanTransaction(coinbaseTx);
		}
	}

	public BitcoinAddress CreateNewAddress(bool isInternal = false)
	{
		var key = CreateNewKey(isInternal);
		var scriptPubKey = key.PrivateKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
		ScriptPubKeys.Add(scriptPubKey, key);
		return scriptPubKey.GetDestinationAddress(Rpc.Network);
	}

	public async Task<uint256> CreateSweepToManyTransactionAsync(IEnumerable<Script> outputsOwn, IEnumerable<Script> outputsForeign)
	{
		var feeRate = new FeeRate(2m);
		var tx = Rpc.Network.CreateTransaction();
		var outputsCount = outputsOwn.Count() + outputsForeign.Count();
		long totalValue = 0;
		foreach (var utxo in Utxos)
		{
			totalValue += utxo.Amount;
			tx.Inputs.Add(utxo.Outpoint);
		}

		var totalSize = tx.Inputs.Count * 69 + outputsCount * 31 + 122;
		var totalFee = GetFeeWithZero(feeRate, totalSize);
		var outputsForEachOwn = (totalValue - outputsForeign.Count() - totalFee) / outputsOwn.Count();
		foreach (var output in outputsOwn)
		{
			tx.Outputs.Add(outputsForEachOwn, output);
		}
		foreach (var output in outputsForeign)
		{
			tx.Outputs.Add(1000, output);
		}

		var txid = await SendRawTransactionAsync(SignTransaction(tx), CancellationToken.None).ConfigureAwait(false);
		return txid;
	}
	public (Transaction, Coin) CreateTemplateTransaction()
	{
		var biggestUtxo = Utxos.MaxBy(x => x.Amount)
			?? throw new InvalidOperationException("No UTXO is available.");
		var tx = Rpc.Network.CreateTransaction();
		tx.Inputs.Add(biggestUtxo.Outpoint);
		return (tx, biggestUtxo);
	}

	public Transaction CreateSelfTransfer(FeeRate feeRate)
	{
		var (tx, spendingCoin) = CreateTemplateTransaction();
		tx.Outputs.Add(spendingCoin.Amount - GetFeeWithZero(feeRate, 31), CreateNewAddress());
		return tx;
	}

	public async Task<uint256> SendRawTransactionAsync(Transaction tx, CancellationToken cancellationToken)
	{
		var txid = await Rpc.SendRawTransactionAsync(tx, cancellationToken).ConfigureAwait(false);
		ScanTransaction(tx);
		return txid;
	}

	public Transaction SignTransaction(Transaction tx)
	{
		var signedTx = tx.Clone();
		var inputTable = signedTx.Inputs.Select(x => x.PrevOut).ToHashSet();
		var inputsToSign = Utxos.Where(x => inputTable.Contains(x.Outpoint));
		var scriptsToSign = inputsToSign.Select(x => x.ScriptPubKey).ToHashSet();
		var secrets = ScriptPubKeys
			.Where(x => scriptsToSign.Contains(x.Key))
			.Select(x => x.Value.PrivateKey.GetBitcoinSecret(Rpc.Network))
			.ToList();
		signedTx.Sign(secrets, inputsToSign);
		return signedTx;
	}

	public ExtPubKey GetSegwitAccountExtPubKey() =>
		ExtKey.Derive(KeyPath.Parse("m/84'/0'/0'")).Neuter();

	public ExtPubKey GetExtPubKey(Script scriptPubKey) =>
		ScriptPubKeys[scriptPubKey].Neuter();

	public Transaction Sign(Transaction transaction, Coin coin, PrecomputedTransactionData precomputeTransactionData)
	{
		if (!ScriptPubKeys.TryGetValue(coin.ScriptPubKey, out var extKey))
		{
			throw new ArgumentException("Destination doesn't belong to this wallet.");
		}

		transaction.Sign(extKey.PrivateKey.GetBitcoinSecret(Rpc.Network), coin);
		return transaction;
	}

	public IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot) =>
		Enumerable.Range(0, count).Select(_ => CreateNewAddress());

	public IEnumerable<IDestination> GetNextInternalDestinations(int count) =>
		Enumerable.Range(0, count).Select(_ => CreateNewAddress(true));

	public void ScanTransaction(Transaction tx)
	{
		var receivedCoins = tx.Outputs.AsIndexedOutputs()
			.Where(x => ScriptPubKeys.ContainsKey(x.TxOut.ScriptPubKey))
			.Select(x => x.ToCoin());

		Utxos.AddRange(receivedCoins);
		Utxos.RemoveAll(x => tx.Inputs.Any(y => y.PrevOut == x.Outpoint));
	}

	private ExtKey CreateNewKey(bool isInternal)
	{
		var path = isInternal ? "84'/0'/0'/1" : "84'/0'/0'/0";
		return ExtKey.Derive(KeyPath.Parse($"{path}/{NextKeyIndex++}"));
	}

	public static Money GetFeeWithZero(FeeRate feeRate, int virtualSize) =>
		feeRate == FeeRate.Zero ? Money.Zero : feeRate.GetFee(virtualSize);
}
