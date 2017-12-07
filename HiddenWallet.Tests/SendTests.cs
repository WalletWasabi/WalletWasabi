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
			Safe safe = await Safe.LoadAsync(password, path, network);
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

				#region Basic

				Debug.WriteLine((await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2PublicKeyHash, account, HdPathType.Receive)).FirstOrDefault().GetDestinationAddress(Network.TestNet).ToString());
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
				if (res.SpentCoins.Sum(x => x.Amount as Money) - res.ActiveOutput.Value == res.Fee) // this happens when change is too small
				{
					Assert.NotNull(res.ChangeOutput);
					Assert.Contains(res.Transaction.Outputs, x => x.Value == res.ChangeOutput.Value);
					Debug.WriteLine($"Change Output: {res.ChangeOutput.Value.ToString(false, true)} {res.ChangeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				}
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
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

				#endregion

				#region SubtractFeeFromAmount

				receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

				getBalanceResult = await walletJob.GetBalanceAsync(account);
				bal = getBalanceResult.Available;
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true,
					subtractFeeFromAmount: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend - res.Fee, res.ActiveOutput.Value);
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
						Assert.Equal(amountToSend - res.Fee, output.Value);
					}
				}
				Assert.True(foundReceive);
				
				#endregion

				#region CustomChange

				receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();
				var customChange = new Key().ScriptPubKey;

				getBalanceResult = await walletJob.GetBalanceAsync(account);
				bal = getBalanceResult.Available;
				amountToSend = (bal.Confirmed + bal.Unconfirmed) / 2;
				res = await walletJob.BuildTransactionAsync(receive, amountToSend, FeeType.Low, account,
					allowUnconfirmed: true,
					subtractFeeFromAmount: true,
					customChangeScriptPubKey: customChange);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Equal(amountToSend - res.Fee, res.ActiveOutput.Value);
				Assert.NotNull(res.ChangeOutput);
				Assert.Equal(customChange, res.ChangeOutput.ScriptPubKey);
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
						Assert.Equal(amountToSend - res.Fee, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion

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

				#endregion

				#region MaxAmount

				receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

				bal = (await walletJob.GetBalanceAsync(account)).Available;

				res = await walletJob.BuildTransactionAsync(receive, Money.Zero, FeeType.Low, account,
					allowUnconfirmed: true);

				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Null(res.ChangeOutput);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");
				
				Assert.Single(res.Transaction.Outputs);
				var maxBuiltTxOutput = res.Transaction.Outputs.Single();
				Assert.Equal(receive, maxBuiltTxOutput.ScriptPubKey);
				Assert.Equal(bal.Confirmed + bal.Unconfirmed - res.Fee, maxBuiltTxOutput.Value);

				#endregion

				#region InputSelection

				receive = (await walletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Receive)).FirstOrDefault();

				bal = (await walletJob.GetBalanceAsync(account)).Available;

				var inputCountBefore = res.SpentCoins.Count();
				res = await walletJob.BuildTransactionAsync(receive, Money.Zero, FeeType.Low, account,
					allowUnconfirmed: true,
					allowedInputs: res.SpentCoins.Where((x, i) => i == 0 || i % 2 == 0).Select(x=>x.Outpoint));

				Assert.True(inputCountBefore >= res.SpentCoins.Count());
				Assert.Equal(res.SpentCoins.Count(), res.Transaction.Inputs.Count);
				Assert.True(res.Success);
				Assert.Empty(res.FailingReason);
				Assert.Equal(receive, res.ActiveOutput.ScriptPubKey);
				Assert.Null(res.ChangeOutput);
				Debug.WriteLine($"Fee: {res.Fee}");
				Debug.WriteLine($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Debug.WriteLine($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Debug.WriteLine($"Active Output: {res.ActiveOutput.Value.ToString(false, true)} {res.ActiveOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Debug.WriteLine($"TxId: {res.Transaction.GetHash()}");
				
				Assert.Single(res.Transaction.Outputs);

				res = await walletJob.BuildTransactionAsync(receive, Money.Zero, FeeType.Low, account,
					allowUnconfirmed: true,
					allowedInputs: new[] { res.SpentCoins.Select(x => x.Outpoint).First() });

				Assert.Single(res.Transaction.Inputs);
				Assert.Single(res.Transaction.Outputs);
				Assert.Single(res.SpentCoins);
				Assert.Null(res.ChangeOutput);

				#endregion
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

        private void WalletJob_ConnectedNodeCountChanged(object sender, int nodeCount)
        {
            var walletJob = sender as WalletJob;
            if (walletJob.MaxConnectedNodeCount == nodeCount)
            {
                Debug.WriteLine(
                    $"{nameof(WalletJob.MaxConnectedNodeCount)} reached: {walletJob.MaxConnectedNodeCount}");
            }
            else Debug.WriteLine($"{nameof(nodeCount)}: {nodeCount}");
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
			Safe safe = await Safe.LoadAsync(password, path, network);
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
