using NBitcoin;
using NBitcoin.DataEncoders;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Extensions;

namespace WalletWasabi.Tests.UnitTests;

public class TestWallet : IKeyChain, IDestinationProvider
{
	public TestWallet(string name, IRPCClient rpc)
	{
		Rpc = rpc;
		ExtKey = new ExtKey(Encoders.Hex.EncodeData(Encoding.UTF8.GetBytes(name)));
	}

	private IRPCClient Rpc { get; }
	private List<Coin> Utxos { get; } = new();
	private ExtKey ExtKey { get; }
	private Dictionary<Script, ExtKey> ScriptPubKeys { get; } = new();
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
		tx.Outputs.Add(spendingCoin.Amount - feeRate.GetFeeWithZero(Constants.P2wpkhOutputVirtualSize), CreateNewAddress());
		return tx;
	}

	public async Task<Transaction> SendToAsync(Money amount, Script scriptPubKey, FeeRate feeRate, CancellationToken cancellationToken)
	{
		const int FinalSignedTxVirtualSize = 222;
		var effectiveOutputCost = amount + feeRate.GetFeeWithZero(FinalSignedTxVirtualSize);
		var tx = CreateSelfTransfer(FeeRate.Zero);

		if (tx.Outputs[0].Value < effectiveOutputCost)
		{
			throw new ArgumentException("Not enough satoshis in input.");
		}

		if (effectiveOutputCost != tx.Outputs[0].Value)
		{
			tx.Outputs[0].Value -= effectiveOutputCost;
			tx.Outputs.Add(amount, scriptPubKey);
		}
		else
		{
			// Sending whole coin.
			tx.Outputs[0].ScriptPubKey = scriptPubKey;
		}
		await SendRawTransactionAsync(SignTransaction(tx), cancellationToken).ConfigureAwait(false);
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

	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData committedData)
	{
		if (!ScriptPubKeys.TryGetValue(destination.ScriptPubKey, out var extKey))
		{
			throw new ArgumentException("Destination doesn't belong to this wallet.");
		}

		using var identificationKey = new Key();
		return OwnershipProof.GenerateCoinJoinInputProof(
				extKey.PrivateKey,
				new OwnershipIdentifier(identificationKey, destination.ScriptPubKey),
				committedData,
				ScriptPubKeyType.Segwit);
	}

	public Transaction Sign(Transaction transaction, Coin coin, PrecomputedTransactionData precomputeTransactionData)
	{
		if (!ScriptPubKeys.TryGetValue(coin.ScriptPubKey, out var extKey))
		{
			throw new ArgumentException("Destination doesn't belong to this wallet.");
		}

		transaction.Sign(extKey.PrivateKey.GetBitcoinSecret(Rpc.Network), coin);
		return transaction;
	}

	public void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts)
	{
		// Test wallet doesn't care
	}

	public IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot) =>
		Enumerable.Range(0, count).Select(_ => CreateNewAddress());

	public IEnumerable<ScriptType> SupportedScriptTypes { get; } = [ScriptType.P2WPKH];

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
}
