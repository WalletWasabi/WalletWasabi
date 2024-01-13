using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.UnitTests;
using WalletWasabi.Blockchain.Analysis;

namespace WalletWasabi.Tests.Helpers;

public static class BitcoinFactory
{
	public static SmartTransaction CreateSmartTransaction(int othersInputCount = 1, int othersOutputCount = 1, int ownInputCount = 0, int ownOutputCount = 0, bool orderByAmount = false)
		=> CreateSmartTransaction(othersInputCount, Enumerable.Repeat(Money.Coins(1m), othersOutputCount), Enumerable.Repeat((Money.Coins(1.1m), 1), ownInputCount), Enumerable.Repeat((Money.Coins(1m), 1), ownOutputCount), orderByAmount);

	public static SmartTransaction CreateSmartTransaction(int othersInputCount, IEnumerable<Money> othersOutputs, IEnumerable<(Money value, int anonset)> ownInputs, IEnumerable<(Money value, int anonset)> ownOutputs, bool orderByAmount = false)
	{
		var km = ServiceFactory.CreateKeyManager();
		return CreateSmartTransaction(othersInputCount, othersOutputs, ownInputs.Select(x => (x.value, x.anonset, CreateHdPubKey(km))), ownOutputs.Select(x => (x.value, x.anonset, CreateHdPubKey(km))), orderByAmount);
	}

	public static SmartTransaction CreateSmartTransaction(int othersInputCount, IEnumerable<Money> othersOutputs, IEnumerable<(Money value, int anonset, HdPubKey hdpk)> ownInputs, IEnumerable<(Money value, int anonset, HdPubKey hdpk)> ownOutputs, bool orderByAmount = false)
		=> CreateSmartTransaction(othersInputCount, othersOutputs.Select(x => new TxOut(x, new Key())), ownInputs, ownOutputs, orderByAmount);

	public static SmartTransaction CreateSmartTransaction(int othersInputCount, IEnumerable<TxOut> othersOutputs, IEnumerable<(Money value, int anonset, HdPubKey hdpk)> ownInputs, IEnumerable<(Money value, int anonset, HdPubKey hdpk)> ownOutputs, bool orderByAmount)
	{
		var tx = Transaction.Create(Network.Main);
		var walletInputs = new HashSet<SmartCoin>();
		var walletOutputs = new HashSet<SmartCoin>();

		var othersOutputSum = othersOutputs.Sum(x => x.Value);
		List<(Money value, int anonset, HdPubKey hdpk)> remainingOwnInputs = ownInputs.ToList();
		if (orderByAmount)
		{
			remainingOwnInputs = ownInputs.OrderByDescending(x => x.value).ToList();

			// If our input is larger than everyone else's outputs sum, then we know it's the largest input and so it can be added to the beginning.
			foreach (var input in remainingOwnInputs)
			{
				if (input.value > othersOutputSum)
				{
					AddInput(tx, walletInputs, input.value, input.anonset, input.hdpk);
				}
				else
				{
					break;
				}
			}

			remainingOwnInputs = remainingOwnInputs.Skip(tx.Inputs.Count).ToList();
		}

		for (int i = 0; i < othersInputCount; i++)
		{
			tx.Inputs.Add(CreateOutPoint());
		}

		foreach (var (value, anonset, hdpk) in remainingOwnInputs)
		{
			AddInput(tx, walletInputs, value, anonset, hdpk);
		}

		var outputs = new List<(TxOut txout, (int anonset, HdPubKey hdpk)? own)>();
		foreach (var output in othersOutputs)
		{
			outputs.Add((output, null));
		}

		foreach (var output in ownOutputs)
		{
			outputs.Add((new TxOut(output.value, output.hdpk.P2wpkhScript), (output.anonset, output.hdpk)));
		}

		if (orderByAmount)
		{
			outputs = outputs.OrderByDescending(x => x.txout.Value).ToList();
		}

		var stx = new SmartTransaction(tx, Height.Mempool);
		var idx = 0u;
		foreach (var output in outputs)
		{
			tx.Outputs.Add(output.txout);

			if (output.own is not null)
			{
				var hdpk = output.own.Value.hdpk;
				var sc = new SmartCoin(stx, idx, hdpk);
				walletOutputs.Add(sc);
			}

			idx++;
		}

		foreach (var sc in walletInputs)
		{
			stx.TryAddWalletInput(sc);
		}
		foreach (var sc in walletOutputs)
		{
			stx.TryAddWalletOutput(sc);
		}

		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		return stx;
	}

	private static void AddInput(Transaction tx, HashSet<SmartCoin> walletInputs, Money value, int anonset, HdPubKey hdpk)
	{
		var sc = CreateSmartCoin(hdpk, value, anonymitySet: anonset);
		tx.Inputs.Add(sc.Outpoint);
		walletInputs.Add(sc);
	}

	public static HdPubKey CreateHdPubKey(KeyManager km)
		=> CreateHdPubKey(km, isInternal: false);

	public static HdPubKey CreateHdPubKey(KeyManager km, bool isInternal)
		=> km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal);

	public static SmartCoin CreateSmartCoin(HdPubKey pubKey, decimal amountBtc, bool confirmed = true, int anonymitySet = 1)
		=> CreateSmartCoin(pubKey, Money.Coins(amountBtc), confirmed, anonymitySet);

	public static SmartCoin CreateSmartCoin(HdPubKey pubKey, Money amount, bool confirmed = true, int anonymitySet = 1)
		=> CreateSmartCoin(Transaction.Create(Network.Main), pubKey, amount, confirmed, anonymitySet);

	public static SmartCoin CreateSmartCoin(Transaction tx, HdPubKey pubKey, Money amount, bool confirmed = true, int anonymitySet = 1)
	{
		var height = confirmed ? new Height(CryptoHelpers.RandomInt(0, 200)) : Height.Mempool;
		pubKey.SetKeyState(KeyState.Used);
		tx.Outputs.Add(new TxOut(amount, pubKey.GetAssumedScriptPubKey()));
		tx.Inputs.Add(CreateOutPoint());
		var stx = new SmartTransaction(tx, height);
		pubKey.SetAnonymitySet(anonymitySet, stx.GetHash());
		var sc = new SmartCoin(stx, (uint)tx.Outputs.Count - 1, pubKey);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(sc);
		return sc;
	}

	public static OutPoint CreateOutPoint()
		=> new(CreateUint256(), (uint)CryptoHelpers.RandomInt(0, 100));

	public static uint256 CreateUint256()
	{
		var rand = new UnsecureRandom();
		var bytes = new byte[32];
		rand.GetBytes(bytes);
		return new uint256(bytes);
	}

	public static Script CreateScript(Key? key = null)
	{
		if (key is null)
		{
			using Key k = new();
			return k.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
		}
		else
		{
			return key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
		}
	}

	/// <summary>
	/// Creates and configures an fake RPC client used to simulate the
	/// interaction with our bitcoin full node RPC server.
	/// </summary>
	public static MockRpcClient GetMockMinimalRpc()
	{
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetMempoolInfoAsync = () => Task.FromResult(
			new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000, // 1 s/b (default value)
				Histogram = Array.Empty<FeeRateGroup>()
			});

		mockRpc.OnEstimateSmartFeeAsync = (target, mode) => Task.FromResult(
			new EstimateSmartFeeResponse()
			{
				Blocks = target,
				FeeRate = new FeeRate(Money.Satoshis(5000))
			});

		// We don't use the result, but we need not to throw NotImplementedException.
		mockRpc.OnGetBlockCountAsync = () => Task.FromResult(0);
		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(365));
		mockRpc.OnGetTxOutAsync = (_, _, _) => null;

		return mockRpc;
	}

	public static BitcoinAddress CreateBitcoinAddress(Network network, Key? key = null)
	{
		// Segwit script has always a destination address, so it cannot be null.
		return CreateScript(key).GetDestinationAddress(network)!;
	}

	public static Transaction CreateTransaction() => CreateSmartTransaction(1, 0, 0, 1).Transaction;

	public static MemoryCache CreateMemoryCache() => new(new MemoryCacheOptions
	{
		SizeLimit = 1_000,
		ExpirationScanFrequency = TimeSpan.FromSeconds(30)
	});
}
