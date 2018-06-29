using System;
using System.Collections.Generic;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Models;
using WalletWasabi.Gui.ViewModels;
using Avalonia;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
		private IReactiveDerivedList<CoinViewModel> _coins;
		private CoinViewModel _selectedCoin;
		private double _clipboardNotificationOpacity;
		private bool _clipboardNotificationVisible;
		private long _disableClipboard;

		public CoinListViewModel(IEnumerable<CoinViewModel> coins, Func<CoinViewModel, CoinViewModel, int> orderer = null)
		{
			Coins = coins.CreateDerivedCollection(c => c, null, orderer, RxApp.MainThreadScheduler);

			this.WhenAnyValue(x => x.SelectedCoin).Subscribe(async coin =>
			{
				if (coin != null)
				{
					await Application.Current.Clipboard.SetTextAsync(coin.TransactionId);
					ClipboardNotificationVisible = true;
					ClipboardNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						await Task.Delay(1000);
						ClipboardNotificationOpacity = 0;
					});
				}
			});
		}

		public CoinViewModel SelectedCoin
		{
			get { return _selectedCoin; }
			set { this.RaiseAndSetIfChanged(ref _selectedCoin, value); }
		}

		public IReactiveDerivedList<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}

		public double ClipboardNotificationOpacity
		{
			get { return _clipboardNotificationOpacity; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value); }
		}

		public bool ClipboardNotificationVisible
		{
			get { return _clipboardNotificationVisible; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value); }
		}
	}
}
