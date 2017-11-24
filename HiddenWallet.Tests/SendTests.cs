using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HiddenWallet.KeyManagement;
using HiddenWallet.Models;
using NBitcoin;
using Xunit;
using HiddenWallet.FullSpv;
using HiddenWallet.FullSpv.Fees;
using System.Collections.Specialized;

namespace HiddenWallet.Tests
{
	public class SendTests
	{
		[Fact]
		public async Task SendTestAsync()
		{
			Network network = Network.TestNet;
			SafeAccount account = new SafeAccount(1);
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"Sending{network}.json");
			const string password = "";
			Safe safe = await Safe.LoadAsync(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

            // create walletjob
            WalletJob walletJob = new WalletJob();
            await walletJob.InitializeAsync(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: account);
            // note some event
            walletJob.ConnectedNodeCountChanged += WalletJob_ConnectedNodeCountChanged;
            walletJob.StateChanged += WalletJob_StateChanged;

			// start syncing
			var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
                // wait until blocks are synced
                while (walletJob.State <= WalletState.SyncingMemPool)
                {
                    await Task.Delay(1000);
                }

                foreach (var r in await walletJob.GetSafeHistoryAsync(account))
                {
                    Debug.WriteLine(r.TransactionId);
                }

				var record = (await walletJob.GetSafeHistoryAsync(account)).FirstOrDefault();
				Debug.WriteLine(record.Confirmed);
				Debug.WriteLine(record.Amount);

				var receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

                var getBalanceResult = await walletJob.GetBalanceAsync(account);
                var bal = getBalanceResult.Available;
                Money amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				var res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend, res.ActiveOutput.Value);
				Assert.NotNull(res.ChangeOutput);
				Assert.Contains(res.Transaction.Outputs, x =>x.Value == res.ChangeOutput.Value);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"Change Output: {res.ChangeOutput.Value.ToString(false, true)} {res.ChangeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");
				
				var foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach(var output in res.Transaction.Outputs)
				{
					if(output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				_txProbArrived = false;
				_prevCount = (await walletJob.GetTrackerAsync()).TrackedTransactions.Count;
                _currentWalletJob = walletJob;
                (await walletJob.GetTrackerAsync()).TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChangedAsync;

				var sendRes = await walletJob.SendTransactionAsync(res.Transaction);
				Assert.True(sendRes.Success);
				Assert.Empty(sendRes.FailingReason);

				while (!_txProbArrived)
				{
					Debug.WriteLine("Waiting for transaction...");
					await Task.Delay(1000);
				}

				Debug.WriteLine("TrackedTransactions collection changed");
				Assert.Contains((await walletJob.GetTrackerAsync()).TrackedTransactions, x => x.Transaction.GetHash() == res.Transaction.GetHash());
				Debug.WriteLine("Transaction arrived");


				receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

				bal = (await walletJob.GetBalanceAsync(account)).Available;
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;

				#region LowFee

				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend, res.ActiveOutput.Value);
				Assert.NotNull(res.ChangeOutput);
				Assert.Contains(res.Transaction.Outputs, x => x.Value == res.ChangeOutput.Value);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"Change Output: {res.ChangeOutput.Value.ToString(false, true)} {res.ChangeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

				#region MediumFee

				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Medium, account,
					allowUnconfirmed: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend, res.ActiveOutput.Value);
				Assert.NotNull(res.ChangeOutput);
				Assert.Contains(res.Transaction.Outputs, x => x.Value == res.ChangeOutput.Value);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"Change Output: {res.ChangeOutput.Value.ToString(false, true)} {res.ChangeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

				#region HighFee

				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.High, account,
					allowUnconfirmed: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend, res.ActiveOutput.Value);
				Assert.NotNull(res.ChangeOutput);
				Assert.Contains(res.Transaction.Outputs, x => x.Value == res.ChangeOutput.Value);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"Change Output: {res.ChangeOutput.Value.ToString(false, true)} {res.ChangeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

				Assert.InRange(res.Fee, Money.Zero, res.Fee);
				Assert.InRange(res.Fee, res.Fee, res.Fee);

				_txProbArrived = false;
				_prevCount = (await walletJob.GetTrackerAsync()).TrackedTransactions.Count;
				_currentWalletJob = walletJob;
				(await walletJob.GetTrackerAsync()).TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChangedAsync;

				sendRes = await walletJob.SendTransactionAsync(res.Transaction);
				Assert.True(sendRes.Success);
				Assert.Empty(sendRes.FailingReason);

				while (_txProbArrived == false)
				{
					Debug.WriteLine("Waiting for transaction...");
					await Task.Delay(1000);
				}

				Debug.WriteLine("TrackedTransactions collection changed");
				Assert.Contains((await walletJob.GetTrackerAsync()).TrackedTransactions, x => x.Transaction.GetHash() == res.Transaction.GetHash());
				Debug.WriteLine("Transaction arrived");
				
				receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

				bal = (await walletJob.GetBalanceAsync(account)).Available;

				res = await walletJob.BuildTransactionAsync(receive, Money.Zero, FeeType.Low, account,
					allowUnconfirmed: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend, res.ActiveOutput.Value);
				Assert.NotNull(res.ChangeOutput);
				Assert.Contains(res.Transaction.Outputs, x => x.Value == res.ChangeOutput.Value);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"Change Output: {res.ChangeOutput.Value.ToString(false, true)} {res.ChangeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(bal.Confirmed + bal.Unconfirmed - res.Fee, output.Value);
					}
				}
				Assert.True(foundReceive);

				_txProbArrived = false;
				_prevCount = (await walletJob.GetTrackerAsync()).TrackedTransactions.Count;
				_currentWalletJob = walletJob;
				(await walletJob.GetTrackerAsync()).TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChangedAsync;

				sendRes = await walletJob.SendTransactionAsync(res.Transaction);
				Assert.True(sendRes.Success);
				Assert.Empty(sendRes.FailingReason);

				while (_txProbArrived == false)
				{
					Debug.WriteLine("Waiting for transaction...");
					await Task.Delay(1000);
				}

				Debug.WriteLine("TrackedTransactions collection changed");
				Assert.Contains((await walletJob.GetTrackerAsync()).TrackedTransactions, x => x.Transaction.GetHash() == res.Transaction.GetHash());
				Debug.WriteLine("Transaction arrived");
			}
			finally
			{
                (await walletJob.GetTrackerAsync()).TrackedTransactions.CollectionChanged -= TrackedTransactions_CollectionChangedAsync;
                
                cts.Cancel();
                await Task.WhenAll(reportTask, walletJobTask);

                walletJob.ConnectedNodeCountChanged -= WalletJob_ConnectedNodeCountChanged;
                walletJob.StateChanged -= WalletJob_StateChanged;

                cts?.Dispose();
                reportTask?.Dispose();
                walletJobTask?.Dispose();
			}
		}

        private void WalletJob_StateChanged(object sender, EventArgs e)
        {
            var walletJob = sender as WalletJob;
            Debug.WriteLine($"{nameof(walletJob.State)}: {walletJob.State}");
        }

        private void WalletJob_ConnectedNodeCountChanged(object sender, EventArgs e)
        {
            var walletJob = sender as WalletJob;
            if (walletJob.MaxConnectedNodeCount == walletJob.ConnectedNodeCount)
            {
                Debug.WriteLine(
                    $"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {walletJob.MaxConnectedNodeCount}");
            }
            else Debug.WriteLine($"{nameof(WalletJob.ConnectedNodeCount)}: {walletJob.ConnectedNodeCount}");
        }

        bool _txProbArrived;
        private int _prevCount;
        WalletJob _currentWalletJob;
        private async void TrackedTransactions_CollectionChangedAsync(object sender, NotifyCollectionChangedEventArgs e)
        {
            var actCount = (await _currentWalletJob.GetTrackerAsync()).TrackedTransactions.Count;
            // if arrived
            if (actCount > _prevCount)
            {
                _txProbArrived = true;
            }
            else _prevCount = actCount;
        }

		[Fact]
		public async Task SendsFailGracefullyTestAsync()
		{
			Network network = Network.TestNet;
			SafeAccount account = new SafeAccount(1);
			string path = Path.Combine(Helpers.CommittedWalletsFolderPath, $"Sending{network}.json");
			const string password = "";
			Safe safe = await Safe.LoadAsync(password, path);
			Assert.Equal(network, safe.Network);
			Debug.WriteLine($"Unique Safe ID: {safe.UniqueId}");

            // create walletjob
            WalletJob walletJob = new WalletJob();
            await walletJob.InitializeAsync(Helpers.SocksPortHandler, Helpers.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: account);
            // note some event
            walletJob.ConnectedNodeCountChanged += WalletJob_ConnectedNodeCountChanged;
            walletJob.StateChanged += WalletJob_StateChanged;

            // start syncing
            var cts = new CancellationTokenSource();
			var walletJobTask = walletJob.StartAsync(cts.Token);
			Task reportTask = Helpers.ReportAsync(cts.Token, walletJob);

			try
			{
				// wait until blocks are synced
				while (walletJob.State <= WalletState.SyncingMemPool)
				{
					await Task.Delay(1000);
				}

				var history = await walletJob.GetSafeHistoryAsync(account);
				foreach(var record in history)
				{
					Debug.WriteLine($"{record.TransactionId} {record.Amount} {record.Confirmed}");
				}

				var receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

                var bal = (await walletJob.GetBalanceAsync(account)).Available;

                // Not enough fee
                Money amountToSend = (bal.Confirmed + bal.Unconfirmed) - new Money(1m, MoneyUnit.Satoshi);
				var res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true);
				Assert.False(res.Success);
				Assert.NotEmpty(res.FailingReason);
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// That's not how you spend all
				amountToSend = (bal.Confirmed + bal.Unconfirmed);
				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true);
				Assert.False(res.Success);
				Assert.NotEmpty(res.FailingReason);
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// Too much
				amountToSend = (bal.Confirmed + bal.Unconfirmed) + new Money(1, MoneyUnit.BTC);
				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true);
				Assert.True(res.Success == false);
				Assert.True(res.FailingReason != "");
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// Minus
				amountToSend = new Money(-1m, MoneyUnit.BTC);
				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true);
				Assert.False(res.Success);
				Assert.NotEmpty(res.FailingReason);
				Debug.WriteLine($"Expected FailingReason: {res.FailingReason}");

				// Default account is disabled
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				await Assert.ThrowsAsync<NotSupportedException>(async () => await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low,
					allowUnconfirmed: true)).ContinueWith(t => {});

				// No such account
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				await Assert.ThrowsAsync<NotSupportedException>(async () => await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, new SafeAccount(23421),
					allowUnconfirmed: true)).ContinueWith(t => { });
			}
			finally
			{
				cts?.Cancel();
				await Task.WhenAll(reportTask, walletJobTask);

                walletJob.ConnectedNodeCountChanged -= WalletJob_ConnectedNodeCountChanged;
                walletJob.StateChanged -= WalletJob_StateChanged;

                cts?.Dispose();
                reportTask?.Dispose();
                walletJobTask?.Dispose();
            }
		}
	}
}
