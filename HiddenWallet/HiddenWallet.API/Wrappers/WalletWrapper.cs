using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
using HBitcoin.MemPool;
using HiddenWallet.API.Models;
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

namespace HiddenWallet.API.Wrappers
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

		private Money _availableAlice = Money.Zero;
		private Money _availableBob = Money.Zero;
		private Money _incomingAlice = Money.Zero;
		private Money _incomingBob = Money.Zero;
		public Money GetAvailable(SafeAccount account) => account == AliceAccount ? _availableAlice : _availableBob;
		public Money GetIncoming(SafeAccount account) => account == AliceAccount ? _incomingAlice : _incomingBob;

		private ReceiveResponse _receiveAddressesAlice = new ReceiveResponse();
		private ReceiveResponse _receiveAddressesBob = new ReceiveResponse();
		public ReceiveResponse GetReceiveResponse(SafeAccount account) => account == AliceAccount ? _receiveAddressesAlice : _receiveAddressesBob;

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

				_walletJob = new WalletJob(safe, trackDefaultSafe: false, accountsToTrack: new SafeAccount[] { AliceAccount, BobAccount });

				_walletJob.StateChanged += _walletJob_StateChanged;
				_walletJob.Tracker.TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChanged;

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
			var aliceHistory = _walletJob.GetSafeHistory(AliceAccount);
			var bobHistory = _walletJob.GetSafeHistory(BobAccount);

			// balances
			CalculateBalances(aliceHistory, out Money aa, out Money ia);
			_availableAlice = aa;
			_incomingAlice = ia;
			CalculateBalances(bobHistory, out Money ab, out Money ib);
			_availableBob = ab;
			_incomingBob = ib;

			// receive
			var ua = _walletJob.GetUnusedScriptPubKeys(AliceAccount, HdPathType.Receive).ToArray();
			var ub = _walletJob.GetUnusedScriptPubKeys(BobAccount, HdPathType.Receive).ToArray();
			_receiveAddressesAlice.Addresses = new string[7];
			_receiveAddressesBob.Addresses = new string[7];
			var network = _walletJob.Safe.Network;
			for (int i = 0; i < 7; i++)
			{
				if (ua[i] != null) _receiveAddressesAlice.Addresses[i] = ua[i].GetDestinationAddress(network).ToWif();
				else _receiveAddressesAlice.Addresses[i] = "";
				if (ub[i] != null) _receiveAddressesBob.Addresses[i] = ub[i].GetDestinationAddress(network).ToWif();
				else _receiveAddressesBob.Addresses[i] = "";
			}
		}

		private static void CalculateBalances(IEnumerable<SafeHistoryRecord> history, out Money available, out Money incoming)
		{
			available = Money.Zero;
			incoming = Money.Zero;
			foreach (var rec in history)
			{
				if (rec.Confirmed)
				{
					available += rec.Amount;
				}
				else
				{
					if (rec.Amount < Money.Zero)
					{
						available += rec.Amount;
					}
					else
					{
						incoming += rec.Amount;
					}
				}
			}
		}

		private void _walletJob_StateChanged(object sender, EventArgs e)
		{
			_walletState = _walletJob.State.ToString();
		}
#endregion

		public async Task EndAsync()
		{
			if (_walletJob != null)
			{
				_walletJob.StateChanged -= _walletJob_StateChanged;
				_walletJob.Tracker.TrackedTransactions.CollectionChanged -= TrackedTransactions_CollectionChanged;
			}

			_cts.Cancel();
			await Task.WhenAll(_walletJobTask).ConfigureAwait(false);
			Console.WriteLine("Graceful shut down...");
		}

		public StatusResponse GetStatusResponse()
		{
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
				
				return new StatusResponse { HeaderHeight = hh, TrackingHeight = th, ConnectedNodeCount = nc, MemPoolTransactionCount = mtxc, WalletState = ws, ChangeBump = cb };
			}
			else return new StatusResponse { HeaderHeight = 0, TrackingHeight = 0, ConnectedNodeCount = 0, MemPoolTransactionCount = 0, WalletState = WalletState.NotStarted.ToString(), ChangeBump = 0 };
		}
	}
}
