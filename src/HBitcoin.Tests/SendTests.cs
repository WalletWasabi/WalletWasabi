using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
using NBitcoin;
using Xunit;
using HBitcoin.Fees;

namespace HBitcoin.Tests
{
	public class SendTests
	{
		[Fact]
		public void BasicSendTest()
		{
			Network network = Network.TestNet;
			SafeAccount account = new SafeAccount(1);
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"Sending{network}.json");
			const string password = "";
			Safe safe = Safe.Load(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: account);
			// note some event
			WalletJob.ConnectedNodeCountChanged += delegate
			{
				if (WalletJob.MaxConnectedNodeCount == WalletJob.ConnectedNodeCount)
				{
					Debug.WriteLine(
						$"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {WalletJob.MaxConnectedNodeCount}");
				}
				else Debug.WriteLine($"{nameof(WalletJob.ConnectedNodeCount)}: {WalletJob.ConnectedNodeCount}");
			};
			walletJob.StateChanged += delegate
			{
				Debug.WriteLine($"{nameof(walletJob.State)}: {walletJob.State}");
			};

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until blocks are synced
				while (walletJob.State <= WalletState.SyncingMemPool)
				{
					Task.Delay(1000).Wait();
				}

				var record = walletJob.GetSafeHistory(account).FirstOrDefault();
				Debug.WriteLine(record.Confirmed);
				Debug.WriteLine(record.Amount);

				var receive = walletJob.GetUnusedScriptPubKeys(account, HdPathType.Receive).FirstOrDefault();

				IDictionary<Coin, bool> unspentCoins;
				var bal = walletJob.GetBalance(out unspentCoins, account);
				Money amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				var res = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true).Result;

				Assert.True(res.Success);
				Assert.True(res.FailingReason == "");
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Transaction: {res.Transaction}");

				var foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach(var output in res.Transaction.Outputs)
				{
					if(output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.True(amountToSend == output.Value);
					}
				}
				Assert.True(foundReceive);

				var txProbArrived = false;
				var prevCount = walletJob.Tracker.TrackedTransactions.Count;
				walletJob.Tracker.TrackedTransactions.CollectionChanged += delegate
				{
					var actCount = walletJob.Tracker.TrackedTransactions.Count;
					// if arrived
					if (actCount > prevCount)
					{
						txProbArrived = true;
					}
					else prevCount = actCount;
				};

				var sendRes = walletJob.SendTransactionAsync(res.Transaction).Result;
				Assert.True(sendRes.Success);
				Assert.True(sendRes.FailingReason == "");

				while (txProbArrived == false)
				{
					Debug.WriteLine("Waiting for transaction...");
					Task.Delay(1000).Wait();
				}

				Debug.WriteLine("TrackedTransactions collection changed");
				Assert.True(walletJob.Tracker.TrackedTransactions.Any(x => x.Transaction.GetHash() == res.Transaction.GetHash()));
				Debug.WriteLine("Transaction arrived");
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();
			}
		}

		[Fact]
		public void FeeTest()
		{
			Network network = Network.TestNet;
			SafeAccount account = new SafeAccount(1);
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"Sending{ network}.json");
			const string password = "";
			Safe safe = Safe.Load(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: account);
			// note some event
			WalletJob.ConnectedNodeCountChanged += delegate
			{
				if (WalletJob.MaxConnectedNodeCount == WalletJob.ConnectedNodeCount)
				{
					Debug.WriteLine(
						$"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {WalletJob.MaxConnectedNodeCount}");
				}
				else Debug.WriteLine($"{nameof(WalletJob.ConnectedNodeCount)}: {WalletJob.ConnectedNodeCount}");
			};
			walletJob.StateChanged += delegate
			{
				Debug.WriteLine($"{nameof(walletJob.State)}: {walletJob.State}");
			};

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until blocks are synced
				while (walletJob.State <= WalletState.SyncingMemPool)
				{
					Task.Delay(1000).Wait();
				}

				var receive = walletJob.GetUnusedScriptPubKeys(account, HdPathType.Receive).FirstOrDefault();

				IDictionary<Coin, bool> unspentCoins;
				var bal = walletJob.GetBalance(out unspentCoins, account);
				Money amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;

				#region LowFee

				var resLow = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true).Result;

				Assert.True(resLow.Success);
				Assert.True(resLow.FailingReason == "");
				Debug.WriteLine($"Fee: {resLow.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {resLow.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {resLow.SpendsUnconfirmed}");
				Debug.WriteLine($"Transaction: {resLow.Transaction}");

				var foundReceive = false;
				Assert.InRange(resLow.Transaction.Outputs.Count, 1, 2);
				foreach (var output in resLow.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.True(amountToSend == output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

				#region MediumFee

				var resMedium = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Medium, account,
					allowUnconfirmed: true).Result;

				Assert.True(resMedium.Success);
				Assert.True(resMedium.FailingReason == "");
				Debug.WriteLine($"Fee: {resMedium.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {resMedium.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {resMedium.SpendsUnconfirmed}");
				Debug.WriteLine($"Transaction: {resMedium.Transaction}");

				foundReceive = false;
				Assert.InRange(resMedium.Transaction.Outputs.Count, 1, 2);
				foreach (var output in resMedium.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.True(amountToSend == output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

				#region HighFee

				var resHigh = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.High, account,
					allowUnconfirmed: true).Result;

				Assert.True(resHigh.Success);
				Assert.True(resHigh.FailingReason == "");
				Debug.WriteLine($"Fee: {resHigh.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {resHigh.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {resHigh.SpendsUnconfirmed}");
				Debug.WriteLine($"Transaction: {resHigh.Transaction}");

				foundReceive = false;
				Assert.InRange(resHigh.Transaction.Outputs.Count, 1, 2);
				foreach (var output in resHigh.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.True(amountToSend == output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

				Assert.True(resLow.Fee <= resMedium.Fee);
				Assert.True(resMedium.Fee <= resHigh.Fee);

				var txProbArrived = false;
				var prevCount = walletJob.Tracker.TrackedTransactions.Count;
				walletJob.Tracker.TrackedTransactions.CollectionChanged += delegate
				{
					var actCount = walletJob.Tracker.TrackedTransactions.Count;
					// if arrived
					if(actCount > prevCount)
					{
						txProbArrived = true;
					}
					else prevCount = actCount;
				};

				var sendRes = walletJob.SendTransactionAsync(resHigh.Transaction).Result;
				Assert.True(sendRes.Success);
				Assert.True(sendRes.FailingReason == "");

				while (txProbArrived == false)
				{
					Debug.WriteLine("Waiting for transaction...");
					Task.Delay(1000).Wait();
				}

				Debug.WriteLine("TrackedTransactions collection changed");
				Assert.True(walletJob.Tracker.TrackedTransactions.Any(x => x.Transaction.GetHash() == resHigh.Transaction.GetHash()));
				Debug.WriteLine("Transaction arrived");
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();
			}
		}

		[Fact]
		public void MaxAmountTest()
		{
			Network network = Network.TestNet;
			SafeAccount account = new SafeAccount(1);
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"Sending{network}.json");
			const string password = "";
			Safe safe = Safe.Load(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: account);
			// note some event
			WalletJob.ConnectedNodeCountChanged += delegate
			{
				if (WalletJob.MaxConnectedNodeCount == WalletJob.ConnectedNodeCount)
				{
					Debug.WriteLine(
						$"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {WalletJob.MaxConnectedNodeCount}");
				}
				else Debug.WriteLine($"{nameof(WalletJob.ConnectedNodeCount)}: {WalletJob.ConnectedNodeCount}");
			};
			walletJob.StateChanged += delegate
			{
				Debug.WriteLine($"{nameof(walletJob.State)}: {walletJob.State}");
			};

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until blocks are synced
				while (walletJob.State <= WalletState.SyncingMemPool)
				{
					Task.Delay(1000).Wait();
				}

				var receive = walletJob.GetUnusedScriptPubKeys(account, HdPathType.Receive).FirstOrDefault();

				IDictionary<Coin, bool> unspentCoins;
				var bal = walletJob.GetBalance(out unspentCoins, account);
				
				var res = walletJob.BuildTransactionAsync(receive, Money.Zero, FeeType.Low, account,
					allowUnconfirmed: true).Result;

				Assert.True(res.Success);
				Assert.True(res.FailingReason == "");
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Transaction: {res.Transaction}");

				var foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.True(bal.Confirmed + bal.Unconfirmed - res.Fee == output.Value);
					}
				}
				Assert.True(foundReceive);

				var txProbArrived = false;
				var prevCount = walletJob.Tracker.TrackedTransactions.Count;
				walletJob.Tracker.TrackedTransactions.CollectionChanged += delegate
				{
					var actCount = walletJob.Tracker.TrackedTransactions.Count;
					// if arrived
					if (actCount > prevCount)
					{
						txProbArrived = true;
					}
					else prevCount = actCount;
				};

				var sendRes = walletJob.SendTransactionAsync(res.Transaction).Result;
				Assert.True(sendRes.Success);
				Assert.True(sendRes.FailingReason == "");

				while (txProbArrived == false)
				{
					Debug.WriteLine("Waiting for transaction...");
					Task.Delay(1000).Wait();
				}

				Debug.WriteLine("TrackedTransactions collection changed");
				Assert.True(walletJob.Tracker.TrackedTransactions.Any(x => x.Transaction.GetHash() == res.Transaction.GetHash()));
				Debug.WriteLine("Transaction arrived");
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();
			}
		}

		[Fact]
		public void SendsFailGracefullyTest()
		{
			Network network = Network.TestNet;
			SafeAccount account = new SafeAccount(1);
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"Sending{network}.json");
			const string password = "";
			Safe safe = Safe.Load(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");
			
			// create walletjob
			WalletJob walletJob = new WalletJob(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: account);
			// note some event
			WalletJob.ConnectedNodeCountChanged += delegate
			{
				if (WalletJob.MaxConnectedNodeCount == WalletJob.ConnectedNodeCount)
				{
					Debug.WriteLine(
						$"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {WalletJob.MaxConnectedNodeCount}");
				}
				else Debug.WriteLine($"{nameof(WalletJob.ConnectedNodeCount)}: {WalletJob.ConnectedNodeCount}");
			};
			walletJob.StateChanged += delegate
			{
				Debug.WriteLine($"{nameof(walletJob.State)}: {walletJob.State}");
			};

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until blocks are synced
				while (walletJob.State <= WalletState.SyncingMemPool)
				{
					Task.Delay(1000).Wait();
				}

				var history = walletJob.GetSafeHistory(account);
				foreach(var record in history)
				{
					Debug.WriteLine($"{record.TransactionId} {record.Amount} {record.Confirmed}");
				}

				var receive = walletJob.GetUnusedScriptPubKeys(account, HdPathType.Receive).FirstOrDefault();

				IDictionary<Coin, bool> unspentCoins;
				var bal = walletJob.GetBalance(out unspentCoins, account);

				// Not enough fee
				Money amountToSend = (bal.Confirmed + bal.Unconfirmed) - new Money(1m, MoneyUnit.Satoshi);
				var res = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true).Result;
				Assert.True(res.Success == false);
				Assert.True(res.FailingReason != "");
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// That's not how you spend all
				amountToSend = (bal.Confirmed + bal.Unconfirmed);
				res = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true).Result;
				Assert.True(res.Success == false);
				Assert.True(res.FailingReason != "");
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// Too much
				amountToSend = (bal.Confirmed + bal.Unconfirmed) + new Money(1, MoneyUnit.BTC);
				res = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true).Result;
				Assert.True(res.Success == false);
				Assert.True(res.FailingReason != "");
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// Minus
				amountToSend = new Money(-1m, MoneyUnit.BTC);
				res = walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true).Result;
				Assert.True(res.Success == false);
				Assert.True(res.FailingReason != "");
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// Default account is disabled
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				Assert.ThrowsAsync<NotSupportedException>(async () => await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, 
					allowUnconfirmed: true).ConfigureAwait(false)).ContinueWith(t => {}).Wait();

				// No such account
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				Assert.ThrowsAsync<NotSupportedException>(async () => await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, new SafeAccount(23421),
					allowUnconfirmed: true).ConfigureAwait(false)).ContinueWith(t => { }).Wait();
			}
			finally
			{
				cts.Cancel();
				Task.WhenAll(reportTask, walletJobTask).Wait();
			}
		}
	}
}
