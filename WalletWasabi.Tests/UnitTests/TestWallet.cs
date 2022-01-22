using NBitcoin;
using NBitcoin.Crypto;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Tests.UnitTests;

public class TestWallet : IKeyChain, IDestinationProvider
{
	public TestWallet(string name, IRPCClient rpc)
	{
		Rpc = rpc;
		Key = new Key(Hashes.SHA256(Encoding.UTF8.GetBytes(name)));
	}

	private IRPCClient Rpc { get; }
	private List<Coin> Utxos { get; } = new ();
	private Key Key { get; }
	public PubKey PubKey => Key.PubKey;
	public Script ScriptPubKey => Key.GetScriptPubKey(ScriptPubKeyType.Segwit);
	public BitcoinAddress Address => Key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Rpc.Network);

	public async Task GenerateAsync(int blocks, CancellationToken cancellationToken)
	{
		var blockIds = await Rpc.GenerateToAddressAsync(blocks, Address, cancellationToken).ConfigureAwait(false);
		foreach(var blockId in blockIds)
		{
			var block = await Rpc.GetBlockAsync(blockId, cancellationToken).ConfigureAwait(false);
			var coinbaseTx = block.Transactions[0];
			ScanTransaction(coinbaseTx);
		}
	}

	public Transaction CreateSelfTransfer(FeeRate feeRate)
	{
		var biggestUtxo = Utxos.MaxBy(x => x.Amount);
		var tx = Rpc.Network.CreateTransaction();
		tx.Inputs.Add(biggestUtxo.Outpoint);
		tx.Outputs.Add(biggestUtxo.Amount - feeRate.GetFee(82), Address);
		return tx;
	}

	public async Task<uint256> SendToAsync(Money amount, Script scriptPubKey, FeeRate feeRate, CancellationToken cancellationToken)
	{
		var cost = feeRate.GetFee(113);
		var tx = CreateSelfTransfer(FeeRate.Zero);
		if (tx.Outputs[0].Value < amount + cost)
		{
			throw new ArgumentException("Not enought satoshis in input.");
		}
		tx.Outputs[0].Value -= (amount + cost);
		tx.Outputs.Add(amount, scriptPubKey);
		return await SendRawTransactionAsync(tx, cancellationToken).ConfigureAwait(false);
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
		signedTx.Sign(Key.GetBitcoinSecret(Rpc.Network), inputsToSign);
		return signedTx;
	}
	private void ScanTransaction(Transaction tx)
	{
		foreach(var indexedOutput in tx.Outputs.AsIndexedOutputs())
		{
			if(indexedOutput.TxOut.ScriptPubKey == ScriptPubKey)
			{
				Utxos.Add(indexedOutput.ToCoin());
			}
		}
	}

	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData commitedData)
	{
		using var identificationKey = new Key();
		return OwnershipProof.GenerateCoinJoinInputProof(
				Key,
				new OwnershipIdentifier(identificationKey, ScriptPubKey),
				commitedData);
	}

	public Transaction Sign(Transaction transaction, Coin coin, OwnershipProof ownershipProof)
	{
		transaction.Sign(Key.GetBitcoinSecret(Rpc.Network), coin);
		return transaction;
	}

	public IEnumerable<IDestination> GetNextDestinations(int count)
	{
		return Enumerable.Repeat(Address, count);
	}
}