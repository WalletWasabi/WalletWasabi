using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
		private ObservableCollection<CoinViewModel> _selectedCoins;
		private ObservableCollection<CoinViewModel> _coins;

		public CoinListViewModel()
		{
			SelectedCoins = new ObservableCollection<CoinViewModel>();

			Coins = new ObservableCollection<CoinViewModel>
			{
				new CoinViewModel(this)
				{
					 AmountBtc = "+0.002",
					 Confirmed = true,
					 IsSelected = false,
					 Label = "TestLabel", PrivacyLevel = 7
				},
				new CoinViewModel(this)
				{
					 AmountBtc = "+0.002",
					 Confirmed = true,
					 IsSelected = false,
					 Label = "TestLabel", PrivacyLevel = 7
				},
				new CoinViewModel(this)
				{
					 AmountBtc = "+0.002",
					 Confirmed = true,
					 IsSelected = false,
					 Label = "TestLabel", PrivacyLevel = 7
				},
				new CoinViewModel(this)
				{
					 AmountBtc = "+0.002",
					 Confirmed = true,
					 IsSelected = false,
					 Label = "TestLabel", PrivacyLevel = 7
				},
				new CoinViewModel(this)
				{
					 AmountBtc = "+0.002",
					 Confirmed = true,
					 IsSelected = false,
					 Label = "TestLabel", PrivacyLevel = 7
				},
				new CoinViewModel(this)
				{
					 AmountBtc = "+0.002",
					 Confirmed = true,
					 IsSelected = false,
					 Label = "TestLabel", PrivacyLevel = 7
				},
			};
		}

		public ObservableCollection<CoinViewModel> SelectedCoins
		{
			get { return _selectedCoins; }
			set { this.RaiseAndSetIfChanged(ref _selectedCoins, value); }
		}

		public ObservableCollection<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
	}
}
