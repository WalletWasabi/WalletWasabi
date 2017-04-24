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
		private SafeAccount aliceAccount = new SafeAccount(1);
		private SafeAccount bobAccount = new SafeAccount(2);

		private CancellationTokenSource _cts = new CancellationTokenSource();
		private Task _walletJobTask = Task.CompletedTask;
		private Task _headerHeightReporter = Task.CompletedTask;
		
		public bool WalletExists => File.Exists(Config.WalletFilePath);
		public StatusResponse StatusResponse = new StatusResponse{ HeaderHeight = 0, TrackingHeight = 0, WalletState = WalletState.NotStarted.ToString() };

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

			_walletJob = new WalletJob(safe, trackDefaultSafe: false, accountsToTrack: new SafeAccount[] { aliceAccount, bobAccount });

			_walletJobTask = _walletJob.StartAsync(_cts.Token);

			_walletJob.BestHeightChanged += _walletJob_BestHeightChanged;
			_walletJob.StateChanged += _walletJob_StateChanged;

			_headerHeightReporter = ReportHeaderHeightAsync(_cts.Token);
		}

		private async Task ReportHeaderHeightAsync(CancellationToken ctsToken)
		{
			while (true)
			{
				try
				{
					await Task.Delay(1000, ctsToken).ContinueWith(t => { }).ConfigureAwait(false);
					if (ctsToken.IsCancellationRequested) return;

					if (WalletJob.TryGetHeaderHeight(out Height height))
					{
						if (height.Type != HeightType.Chain)
							StatusResponse.HeaderHeight = 0;
						else StatusResponse.HeaderHeight = height.Value;
					}
				}
				catch (OperationCanceledException)
				{

				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(ReportHeaderHeightAsync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}
		private void _walletJob_StateChanged(object sender, EventArgs e)
		{
			StatusResponse.WalletState = _walletJob.State.ToString();
		}
		private void _walletJob_BestHeightChanged(object sender, EventArgs e)
		{
			var height = _walletJob.BestHeight;
			if (height.Type != HeightType.Chain)
				StatusResponse.TrackingHeight = 0;
			else StatusResponse.TrackingHeight = height.Value;
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
			if (_walletJob != null)
			{
				_walletJob.BestHeightChanged -= _walletJob_BestHeightChanged;
				_walletJob.StateChanged -= _walletJob_StateChanged;
			}

			await Task.WhenAll(_walletJobTask, _headerHeightReporter).ConfigureAwait(false);
		}
	}
}
