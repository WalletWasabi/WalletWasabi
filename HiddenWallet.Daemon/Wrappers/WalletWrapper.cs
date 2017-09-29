using HiddenWallet.FullSpv;
using HiddenWallet.KeyManagement;
using HiddenWallet.Models;
using HiddenWallet.FullSpv.MemPool;
using HiddenWallet.Daemon.Models;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HiddenWallet.FullSpv.WalletJob;
using DotNetTor.SocksPort;
using HiddenWallet.FullSpv.Fees;

namespace HiddenWallet.Daemon.Wrappers
{
	public class WalletWrapper
	{
#region Members
		private int _changeBump = 0; // every time a change happens this value is bumped
		private string _walletState = WalletState.NotStarted.ToString();

		private string _password = null;
		private WalletJob _walletJob = null;
		public readonly SafeAccount AliceAccount = new SafeAccount(1);
		public readonly SafeAccount BobAccount = new SafeAccount(2);

		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private Task _walletJobTask = Task.CompletedTask;
		
		public bool WalletExists => File.Exists(Config.WalletFilePath);
		public bool IsDecrypted => !_walletJobTask.IsCompleted && _password != null;

		public Network Network => _walletJob.Safe.Network;

		private Money _availableAlice = Money.Zero;
		private Money _availableBob = Money.Zero;
		private Money _incomingAlice = Money.Zero;
		private Money _incomingBob = Money.Zero;
		public Money GetAvailable(SafeAccount account) => account == AliceAccount ? _availableAlice : _availableBob;
		public Money GetIncoming(SafeAccount account) => account == AliceAccount ? _incomingAlice : _incomingBob;

		private ReceiveResponse _receiveResponseAlice = new ReceiveResponse();
		private ReceiveResponse _receiveResponseBob = new ReceiveResponse();
		public ReceiveResponse GetReceiveResponse(SafeAccount account) => account == AliceAccount ? _receiveResponseAlice : _receiveResponseBob;

		private HistoryResponse _historyResponseAlice = new HistoryResponse();
		private HistoryResponse _historyResponseBob = new HistoryResponse();
		public HistoryResponse GetHistoryResponse(SafeAccount account) => account == AliceAccount ? _historyResponseAlice : _historyResponseBob;

		#endregion

		public WalletWrapper()
		{
			// Loads the config file
			// It also creates it with default settings if doesn't exist
			Config.Load();
		}

#region SafeOperations
		public WalletCreateResponse Create(string password)
		{
			var safe = Safe.Create(out Mnemonic mnemonic, password, Config.WalletFilePath, Config.Network);
			return new WalletCreateResponse
			{
				Mnemonic = mnemonic.ToString(),
				CreationTime = safe.GetCreationTimeString()
			};
		}

		public void Load(string password)
		{
			Safe safe = Safe.Load(password, Config.WalletFilePath);
			if (safe.Network != Config.Network) throw new NotSupportedException("Network in the config file differs from the netwrok in the wallet file");

			if (!_walletJobTask.IsCompleted)
			{
				// then it's already running, because the default walletJobTask is completedtask
				if (_password != password) throw new NotSupportedException("Passwords don't match");
			}
			else
			{
				// it's not running yet, let's run it
				_password = password;

				_walletJob = new WalletJob(Tor.SocksPortHandler, Tor.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: new SafeAccount[] { AliceAccount, BobAccount });

				_walletJob.StateChanged += _walletJob_StateChanged;
				_walletJob.Tracker.TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChanged;

				_receiveResponseAlice.ExtPubKey = _walletJob.Safe.GetBitcoinExtPubKey(index: null, hdPathType: HdPathType.NonHardened, account: AliceAccount).ToWif();
				_receiveResponseBob.ExtPubKey = _walletJob.Safe.GetBitcoinExtPubKey(index: null, hdPathType: HdPathType.NonHardened, account: BobAccount).ToWif();

				_walletJobTask = _walletJob.StartAsync(_cts.Token);

				UpdateHistoryRelatedMembers();
			}
		}

		public void Recover(string password, string mnemonic, string creationTime)
		{
			Safe.Recover(
				new Mnemonic(mnemonic),
				password,
				Config.WalletFilePath,
				Config.Network,
				DateTimeOffset.ParseExact(creationTime, "yyyy-MM-dd", CultureInfo.InvariantCulture));
		}

		#endregion

#region EventSubscriptions
		private void TrackedTransactions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			UpdateHistoryRelatedMembers();

			// changeBump
			if (_changeBump >= 10000)
			{
				_changeBump = 0;
			}
			else
			{
				_changeBump++;
			}
		}

		private void UpdateHistoryRelatedMembers()
		{
			// history
			var aliceHistory = _walletJob.GetSafeHistory(AliceAccount).OrderByDescending(x=> x.TimeStamp);
			var bobHistory = _walletJob.GetSafeHistory(BobAccount).OrderByDescending(x => x.TimeStamp);

			var hra = new List<HistoryRecordModel>();
			foreach (var rec in aliceHistory)
			{
				string height;
				if (rec.BlockHeight.Type == HeightType.Chain)
				{
					height = rec.BlockHeight.Value.ToString();
				}
				else height = "";

				hra.Add(new HistoryRecordModel {
					Amount = rec.Amount.ToString(true, true),
					Confirmed = rec.Confirmed,
					Height = height,
					TxId = rec.TransactionId.ToString()
				});
			}
			_historyResponseAlice.History = hra.ToArray();

			var hrb = new List<HistoryRecordModel>();
			foreach (var rec in bobHistory)
			{
				string height;
				if (rec.BlockHeight.Type == HeightType.Chain)
				{
					height = rec.BlockHeight.Value.ToString();
				}
				else height = "";

				hrb.Add(new HistoryRecordModel
				{
					Amount = rec.Amount.ToString(true, true),
					Confirmed = rec.Confirmed,
					Height = height,
					TxId = rec.TransactionId.ToString()
				});
			}
			_historyResponseBob.History = hrb.ToArray();

			// balances
			var aa = _walletJob.GetBalance(out IDictionary<Coin, bool> unspentCoinsAlice, AliceAccount);
			_availableAlice = aa.Confirmed;
			_incomingAlice = aa.Unconfirmed;
			var ab = _walletJob.GetBalance(out IDictionary<Coin, bool> unspentCoinsBob, BobAccount);
			_availableBob = ab.Confirmed;
			_incomingBob = ab.Unconfirmed;
			
			// receive
			var ua = _walletJob.GetUnusedScriptPubKeys(AliceAccount, HdPathType.Receive).ToArray();
			var ub = _walletJob.GetUnusedScriptPubKeys(BobAccount, HdPathType.Receive).ToArray();
			_receiveResponseAlice.Addresses = new string[7];
			_receiveResponseBob.Addresses = new string[7];
			var network = _walletJob.Safe.Network;
			for (int i = 0; i < 7; i++)
			{
				if (ua[i] != null) _receiveResponseAlice.Addresses[i] = ua[i].GetDestinationAddress(network).ToString();
				else _receiveResponseAlice.Addresses[i] = "";
				if (ub[i] != null) _receiveResponseBob.Addresses[i] = ub[i].GetDestinationAddress(network).ToString();
				else _receiveResponseBob.Addresses[i] = "";
			}
		}
		private void _walletJob_StateChanged(object sender, EventArgs e)
		{
			_walletState = _walletJob.State.ToString();
		}
		
		#endregion

		public async Task EndAsync()
		{
			Console.WriteLine("Gracefully shutting down...");
			if (_walletJob != null)
			{
				_walletJob.StateChanged -= _walletJob_StateChanged;
				_walletJob.Tracker.TrackedTransactions.CollectionChanged -= TrackedTransactions_CollectionChanged;
			}

			_cts.Cancel();
			await Task.WhenAll(_walletJobTask).ConfigureAwait(false);
			
			Tor.Kill();
		}		

		public StatusResponse GetStatusResponse()
		{
			var ts = Tor.State.ToString();
			if (_walletJob != null)
			{
				var hh = 0;
				if (WalletJob.TryGetHeaderHeight(out Height headerHeight))
				{
					if (headerHeight.Type == HeightType.Chain)
					{
						hh = headerHeight.Value;
					}
				}

				var bh = _walletJob.BestHeight;
				var th = 0;
				if (bh.Type == HeightType.Chain)
				{
					th = bh.Value;
				}

				var ws = _walletState;

				var nc = WalletJob.ConnectedNodeCount;
				
				var mtxc = MemPoolJob.Transactions.Count;

				var cb = _changeBump;
				
				return new StatusResponse { HeaderHeight = hh, TrackingHeight = th, ConnectedNodeCount = nc, MemPoolTransactionCount = mtxc, WalletState = ws, TorState = ts, ChangeBump = cb };
			}
			else return new StatusResponse { HeaderHeight = 0, TrackingHeight = 0, ConnectedNodeCount = 0, MemPoolTransactionCount = 0, WalletState = WalletState.NotStarted.ToString(), TorState = ts, ChangeBump = 0 };
		}

		public BaseResponse BuildTransaction(string password, SafeAccount safeAccount, BitcoinAddress address, Money amount, FeeType feeType)
		{
			if (password != _password) throw new InvalidOperationException("Wrong password");
			var result = _walletJob.BuildTransactionAsync(address.ScriptPubKey, amount, feeType, safeAccount, Config.CanSpendUnconfirmed).Result;

			if (result.Success)
			{
				return new BuildTransactionResponse
				{
					SpendsUnconfirmed = result.SpendsUnconfirmed,
					Fee = result.Fee.ToString(false, true),
					FeePercentOfSent = result.FeePercentOfSent.ToString("0.##"),
					Hex = result.Transaction.ToHex(),
					Transaction = result.Transaction.ToString()
				};
			}
			else
			{
				return new FailureResponse
				{
					Message = result.FailingReason
				};
			}
		}

		public async Task<BaseResponse> SendTransactionAsync(Transaction tx)
		{
			SendTransactionResult result = await _walletJob.SendTransactionAsync(tx).ConfigureAwait(false);

			if (result.Success) return new SuccessResponse();
			else return new FailureResponse { Message = result.FailingReason, Details = "" };
		}
	}
}
