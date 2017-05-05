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
		private int _trackingHeight = 0;
		private string _walletState = WalletState.NotStarted.ToString();

		private string _password = null;
		private WalletJob _walletJob = null;
		private readonly SafeAccount _aliceAccount = new SafeAccount(1);
		private readonly SafeAccount _bobAccount = new SafeAccount(2);

		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private Task _walletJobTask = Task.CompletedTask;
		
		public bool WalletExists => File.Exists(Config.WalletFilePath);
		public bool IsDecrypted => !_walletJobTask.IsCompleted && _password != null;

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

				_walletJob = new WalletJob(safe, trackDefaultSafe: false, accountsToTrack: new SafeAccount[] { _aliceAccount, _bobAccount });

				_walletJob.BestHeightChanged += _walletJob_BestHeightChanged;
				_walletJob.StateChanged += _walletJob_StateChanged;
				_walletJob.Tracker.TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChanged;

				_walletJobTask = _walletJob.StartAsync(_cts.Token);				
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
			if(_changeBump >= 10000)
			{
				_changeBump = 0;
			}
			else
			{
				_changeBump++;
			}
		}

		private void _walletJob_StateChanged(object sender, EventArgs e)
		{
			_walletState = _walletJob.State.ToString();
		}

		private void _walletJob_BestHeightChanged(object sender, EventArgs e)
		{
			var trackingHeight = _walletJob.BestHeight;
			if (trackingHeight.Type == HeightType.Chain)
			{
				_trackingHeight = trackingHeight.Value;
			}
		}
#endregion

		public async Task EndAsync()
		{
			if (_walletJob != null)
			{
				_walletJob.BestHeightChanged -= _walletJob_BestHeightChanged;
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

				var th = _trackingHeight;

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
