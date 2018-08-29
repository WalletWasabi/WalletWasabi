using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using WalletWasabi.Models;

namespace WalletWasabi.Gui
{
	class CoinEventArgs : EventArgs
	{
		public SmartCoin Coin { get; }

		public CoinEventArgs(SmartCoin coin)
		{
			Coin = coin;
		}
	}

	class CoinsWatcher
	{
		private List<SmartCoin> _trackedCoins;
		private bool _isSynchronized = false;
		public EventHandler<CoinEventArgs> CoinReceived;

		public CoinsWatcher()
		{
			_trackedCoins = new List<SmartCoin>();
			OnBestHeightChanged(this, Height.Unknown);
		}

		public void Start()
		{
			Global.IndexDownloader.BestHeightChanged += OnBestHeightChanged;
			Global.WalletService.Coins.HashSetChanged += OnCoinsArrives;
		}

		public void Stop()
		{
			Global.IndexDownloader.BestHeightChanged -= OnBestHeightChanged;
			Global.WalletService.Coins.HashSetChanged -= OnCoinsArrives;
		}
		private void OnBestHeightChanged(object sender, Height e)
		{
			var filterLeft = Global.IndexDownloader.GetFiltersLeft();
			if(filterLeft == 0)
			{
				Global.IndexDownloader.BestHeightChanged -= OnBestHeightChanged;
				_trackedCoins = Global.WalletService.Coins.ToList();
				_isSynchronized = true;
			}
		}

		private void OnCoinsArrives(object sender, EventArgs e)
		{
			var allCoins = Global.WalletService.Coins;
			
			foreach(var coin in allCoins)
			{
				if( !_trackedCoins.Contains(coin) && _isSynchronized)
				{
					CoinReceived?.Invoke(this, new CoinEventArgs(coin));
				}
			}
			_trackedCoins = allCoins.ToList();
		}
	}

	class Notifier
	{
		private CoinsWatcher _watcher;

		public static readonly Notifier Current = new Notifier();

		private Notifier()
		{
			_watcher = new CoinsWatcher();
		}

		public void Start()
		{
			_watcher.Start();
			_watcher.CoinReceived += NotifyIncomingCoin;
		}

		public void Stop()
		{
			_watcher.CoinReceived -= NotifyIncomingCoin;	
			_watcher.Stop();
		}

		private void NotifyIncomingCoin(object sender, CoinEventArgs e)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				Process.Start(new ProcessStartInfo { 
					FileName = "notify-send", 
					Arguments = $"\"Wasabi Wallet\" \"You have received an incoming transaction {e.Coin.Amount} BTC\"", 
					CreateNoWindow = true
					});
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				Process.Start(new ProcessStartInfo { 
					FileName = "osascript", 
					Arguments = $"-e display notification \"You have received an incoming transaction {e.Coin.Amount} BTC\" with title \"Wasabi Wallet\"", 
					CreateNoWindow = true 
					});
			}
		}
	}
}