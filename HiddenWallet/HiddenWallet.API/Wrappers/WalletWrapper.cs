using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
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
		private string _password = null;
		private WalletJob _walletJob = null;
		private readonly SafeAccount _aliceAccount = new SafeAccount(1);
		private readonly SafeAccount _bobAccount = new SafeAccount(2);

		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private Task _walletJobTask = Task.CompletedTask;
		
		public bool WalletExists => File.Exists(Config.WalletFilePath);

		public WalletWrapper()
		{
			// Loads the config file
			// It also creates it with default settings if doesn't exist
			Config.Load();
		}

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
			_password = password;

			_walletJob = new WalletJob(safe, trackDefaultSafe: false, accountsToTrack: new SafeAccount[] { _aliceAccount, _bobAccount });

			_walletJobTask = _walletJob.StartAsync(_cts.Token);
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

		public async Task ShutdownAsync()
		{
			_cts.Cancel();
			await Task.WhenAll(_walletJobTask).ConfigureAwait(false);
			Console.WriteLine("Gracefully shut down...");
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

				var th = 0;
				var trackingHeight = _walletJob.BestHeight;
				if (trackingHeight.Type == HeightType.Chain)
				{
					th = trackingHeight.Value;
				}

				var ws = _walletJob.State.ToString();
				
				return new StatusResponse { HeaderHeight = hh, TrackingHeight = th, WalletState = ws };
			}
			else return new StatusResponse { HeaderHeight = 0, TrackingHeight = 0, WalletState = WalletState.NotStarted.ToString() };
		}
	}
}
