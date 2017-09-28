using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
using NBitcoin;
using Xunit;
using Xunit.Abstractions;
using System.Diagnostics;
using QBitNinja.Client;
using QBitNinja.Client.Models;

namespace HBitcoin.Tests
{
	public class FullSpvWalletTests
	{
		private bool _fullyConnected;
		private bool _syncedOnce;
		[Theory]
		[InlineData("TestNet")]
		[InlineData("Main")]
		public void SyncingTest(string networkString)
		{
			// load wallet
			Network network = networkString == "TestNet"? Network.TestNet:Network.Main;
			string path = $"Wallets/Empty{network}.json";
			const string password = "";
			Safe safe;
			if(File.Exists(path))
			{
				safe = Safe.Load(password, path);
			}
			else
			{
				Mnemonic mnemonic;
				safe = Safe.Create(out mnemonic, password, path, network);
			}

			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe);

			// note some event
			_fullyConnected = false;
			_syncedOnce = false;
			WalletJob.ConnectedNodeCountChanged += WalletJob_ConnectedNodeCountChanged;
			walletJob.StateChanged += WalletJob_StateChanged;

			Assert.True(walletJob.SafeAccounts.Count == 0);
			Assert.True(WalletJob.ConnectedNodeCount == 0);
			var allTxCount = walletJob.Tracker.TrackedTransactions.Count;
			Assert.True(allTxCount == 0);
			Assert.True(!walletJob.GetSafeHistory().Any());
			Assert.True(walletJob.State == WalletState.NotStarted);
			Assert.True(walletJob.TracksDefaultSafe);

			// start syncing
			var cts = new CancellationTokenSource();
            var walletJobTask = walletJob.StartAsync(cts.Token);
            Assert.True(walletJob.State != WalletState.NotStarted);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until fully synced and connected
				while (!_fullyConnected)
				{
					Task.Delay(10).Wait();
				}

				while (!_syncedOnce)
				{
					Task.Delay(1000).Wait();
				}

				Assert.True(walletJob.State == WalletState.Synced);
				Assert.True(walletJob.CreationHeight != Height.Unknown);
				Assert.True(walletJob.Tracker.TrackedTransactions.Count == 0);
				Assert.True(!walletJob.GetSafeHistory().Any());
				Height headerHeight;
				Assert.True(WalletJob.TryGetHeaderHeight(out headerHeight));
				var expectedBlockCount = headerHeight.Value - walletJob.CreationHeight.Value + 1;
				Assert.True(walletJob.Tracker.BlockCount == expectedBlockCount);
				Assert.True(walletJob.Tracker.TrackedScriptPubKeys.Count > 0);
				Assert.True(walletJob.Tracker.TrackedTransactions.Count == 0);
				
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();

				WalletJob.ConnectedNodeCountChanged -= WalletJob_ConnectedNodeCountChanged;
				walletJob.StateChanged -= WalletJob_StateChanged;
			}
		}

		private void WalletJob_StateChanged(object sender, EventArgs e)
		{
			var walletJob = sender as WalletJob;
			Debug.WriteLine($"{nameof(WalletJob.State)}: {walletJob.State}");
			if (walletJob.State == WalletState.Synced)
			{
				_syncedOnce = true;
			}
			else _syncedOnce = false;
		}

		private void WalletJob_ConnectedNodeCountChanged(object sender, EventArgs e)
		{
			if (WalletJob.MaxConnectedNodeCount == WalletJob.ConnectedNodeCount)
			{
				_fullyConnected = true;
				Debug.WriteLine(
					$"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {WalletJob.MaxConnectedNodeCount}");
			}
			else Debug.WriteLine($"{nameof(WalletJob.ConnectedNodeCount)}: {WalletJob.ConnectedNodeCount}");
		}

		[Fact]
		public void HaveFundsTest()
		{
			// load wallet
			Network network = Network.TestNet;
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"HaveFunds{network}.json");
			const string password = "";
			Safe safe = Safe.Load(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe);
			// note some event
			WalletJob.ConnectedNodeCountChanged += WalletJob_ConnectedNodeCountChanged;
			walletJob.StateChanged += WalletJob_StateChanged;

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until synced enough to have our transaction
				while (walletJob.BestHeight.Type != HeightType.Chain || walletJob.BestHeight < 1092448)
				{
					Task.Delay(1000).Wait();
				}

				var hasMoneyAddress = BitcoinAddress.Create("mmVZjqZjmLvxc3YFhWqYWoe5anrWVcoJcc");
				Debug.WriteLine($"Checking proper balance on {hasMoneyAddress.ToString()}");

				var record = walletJob.GetSafeHistory().FirstOrDefault();
				Assert.True(record != default(SafeHistoryRecord));

				Assert.True(record.Confirmed);
				Assert.True(record.Amount == new Money(0.1m, MoneyUnit.BTC));
				DateTimeOffset expTime;
				DateTimeOffset.TryParse("2017.03.06. 16:47:15 +00:00", out expTime);
				Assert.True(record.TimeStamp == expTime);
				Assert.True(record.TransactionId == new uint256("50898694f281ed059fa6b9d37ccf099ab261540be14fd43ce1a6d6684fbd4e94"));
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();

				WalletJob.ConnectedNodeCountChanged -= WalletJob_ConnectedNodeCountChanged;
				walletJob.StateChanged -= WalletJob_StateChanged;
			}
		}

		// test with a long time used testnet wallet, with exotic, including tumblebit transactions
		[Fact]
		public void RealHistoryTest()
		{
			// load wallet
			Network network = Network.TestNet;
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"HiddenWallet.json");
			const string password = "";
			// I change it because I am using a very old wallet to test
			Safe.EarliestPossibleCreationTime = DateTimeOffset.ParseExact("2016-12-18", "yyyy-MM-dd", CultureInfo.InvariantCulture);
			Safe safe = Safe.Load(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe)
			{
				MaxCleanAddressCount = 79
			};
			// note some event
			WalletJob.ConnectedNodeCountChanged += WalletJob_ConnectedNodeCountChanged;
			_syncedOnce = false;
			walletJob.StateChanged += WalletJob_StateChanged;

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until fully synced
				while (!_syncedOnce)
				{
					Task.Delay(1000).Wait();
				}

				Helpers.ReportFullHistory(walletJob);

				// 0. Query all operations, grouped our used safe addresses
				int MinUnusedKeyNum = 37;
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = Helpers.QueryOperationsPerSafeAddressesAsync(new QBitNinjaClient(safe.Network), safe, MinUnusedKeyNum).Result;

				Dictionary<uint256, List<BalanceOperation>> operationsPerTransactions = QBitNinjaJutsus.GetOperationsPerTransactions(operationsPerAddresses);

				// 3. Create history records from the transactions
				// History records is arbitrary data we want to show to the user
				var txHistoryRecords = new List<Tuple<DateTimeOffset, Money, int, uint256>>();
				foreach (var elem in operationsPerTransactions)
				{
					var amount = Money.Zero;
					foreach (var op in elem.Value)
						amount += op.Amount;

					var firstOp = elem.Value.First();

					txHistoryRecords
						.Add(new Tuple<DateTimeOffset, Money, int, uint256>(
							firstOp.FirstSeen,
							amount,
							firstOp.Confirmations,
							elem.Key));
				}

				// 4. Order the records by confirmations and time (Simply time does not work, because of a QBitNinja issue)
				var qBitHistoryRecords = txHistoryRecords
					.OrderByDescending(x => x.Item3) // Confirmations
					.ThenBy(x => x.Item1); // FirstSeen

				var fullSpvHistoryRecords = walletJob.GetSafeHistory();

				// This won't be equal QBit doesn't show us this transaction: 2017.01.04. 16:24:49	0.00000000	True		77b10ff78aab2e41764a05794c4c464922c73f0c23356190429833ce68fd7be9
				// Assert.Equal(qBitHistoryRecords.Count(), fullSpvHistoryRecords.Count());

				HashSet<SafeHistoryRecord> qBitFoundItToo = new HashSet<SafeHistoryRecord>();
				// Assert all record found by qbit also found by spv and they are identical
				foreach (var record in qBitHistoryRecords)
				{
					// Item2 is the Amount
					SafeHistoryRecord found = fullSpvHistoryRecords.FirstOrDefault(x => x.TransactionId == record.Item4);
					Assert.True(found != default(SafeHistoryRecord));
					Assert.True(found.TimeStamp.Equals(record.Item1));
					Assert.True(found.Confirmed.Equals(record.Item3 > 0));
					Assert.True(found.Amount.Equals(record.Item2));
					qBitFoundItToo.Add(found);
				}

				foreach (var record in fullSpvHistoryRecords)
				{
					if (!qBitFoundItToo.Contains(record))
					{
						Assert.True(null == qBitHistoryRecords.FirstOrDefault(x => x.Item4 == record.TransactionId));
						Debug.WriteLine($@"QBitNinja failed to find, but SPV found it: {record.TimeStamp.DateTime}	{record.Amount}	{record.Confirmed}		{record.TransactionId}");
					}
				}
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();

				WalletJob.ConnectedNodeCountChanged -= WalletJob_ConnectedNodeCountChanged;
				walletJob.StateChanged -= WalletJob_StateChanged;
			}
		}
	}
}
