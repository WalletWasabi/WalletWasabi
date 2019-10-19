using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NBitcoin;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.Services;
using System.IO;
using WalletWasabi.Helpers;
using System.Runtime.CompilerServices;
using NBitcoin.Protocol;
using WalletWasabi.Stores;
using System.Net;

namespace WalletWasabi.Bench
{
	[ClrJob(baseline: true), CoreJob]
	[RPlotExporter, RankColumn]
	public class TransactionProcessorBench
	{
		private TransactionProcessor _tp;
		private List<SmartTransaction> _txs;

		[Params(100, 1_000, 10_000)]
		public int TRANSACTIONS;

		[GlobalSetup]
		public void Setup()
		{
			var random = new Random(Seed:145);
			var utxos = new List<Coin>();
			_tp = CreateTransactionProcessorAsync().Result;

			// Crediting transactions
			for(var i = 0; i < 100; i++)
			{
				var stx = CreateCreditingTransaction(GetP2wpkhScript(_tp), Money.Coins(2.0m), true);
				utxos.Add(stx.Transaction.Outputs.AsCoins().First());
				_tp.Process(stx);
			}

			_txs = new List<SmartTransaction>(TRANSACTIONS);
			for(var i = 0; i < TRANSACTIONS; i++)
			{
				var numOfCoinsToSpend = Math.Min(random.Next(1, 3), utxos.Count());
				var coinsToSpend = utxos.Take(numOfCoinsToSpend);
				var stx = CreateSpendingTransaction(coinsToSpend, new Key().PubKey.ScriptPubKey, GetP2wpkhScript(_tp));
				utxos.Add(stx.Transaction.Outputs.AsCoins().Last());
				_txs.Add(stx);
			}
		}

		[Benchmark]
		public void Process()
		{
			foreach(var stx in _txs)
			{
				_tp.Process(stx);
			}
		}

		private async Task<TransactionProcessor> CreateTransactionProcessorAsync([CallerMemberName] string callerName = "")
		{
			var datadir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Bench"));
			var dir = Path.Combine(datadir, callerName, "TransactionStore");
			Console.WriteLine(dir);
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dir);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Network.Main);
			var bitcoinStore = new BitcoinStore();
			var serviceConfiguration = new ServiceConfiguration(2, 2, 21, 50, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 45678), Money.Coins(0.0001m));

			// 2. Create wasabi synchronizer service.
			var synchronizer = new WasabiSynchronizer(Network.Main, bitcoinStore, ()=>new Uri("http://localhost:35474"), null);
			synchronizer.Start(requestInterval: TimeSpan.FromDays(1), TimeSpan.FromDays(1), 1000);

			// 3. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, "password");

			// 4. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, Network.Main, keyManager, ()=>new Uri("http://localhost:354874"), null);

			// 5. Create wallet service.
			await bitcoinStore.InitializeAsync(dir, Network.Main);

			var workDir = Path.Combine(datadir, EnvironmentHelpers.GetMethodName());
			var wallet = new WalletService(bitcoinStore, keyManager, synchronizer, chaumianClient, nodes, workDir, serviceConfiguration);
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
			{
				await wallet.InitializeAsync(cts.Token); 
			}

			return wallet.TransactionProcessor;
		}

		private static SmartTransaction CreateSpendingTransaction(IEnumerable<Coin> coins, Script scriptPubKey, Script scriptPubKeyChange)
		{
			var tx = Network.RegTest.CreateTransaction();
			var amount = Money.Zero;
			foreach (var coin in coins)
			{
				tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty);
				amount += coin.Amount;
			}
			tx.Outputs.Add(amount.Percentage(60), scriptPubKey ?? Script.Empty);
			tx.Outputs.Add(amount.Percentage(40), scriptPubKeyChange);
			return new SmartTransaction(tx, Height.Mempool);
		}

		private static SmartTransaction CreateCreditingTransaction(Script scriptPubKey, Money amount, bool isConfirmed = false)
		{
			var tx = Network.RegTest.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Outputs.Add(amount, scriptPubKey);
			return new SmartTransaction(tx, isConfirmed ? new Height(9999) : Height.Mempool);
		}

		private static OutPoint GetRandomOutPoint()
		{
			return new OutPoint(RandomUtils.GetUInt256(), 0);
		}

		private static Script GetP2wpkhScript(TransactionProcessor me)
		{
			var label = RandomString.Generate(10);
			return me.KeyManager.GenerateNewKey(new SmartLabel(label), KeyState.Clean, true).P2wpkhScript;
		}
	}
}