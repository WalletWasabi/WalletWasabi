using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _coinList;
		private string _buildTransactionButtonText;
		private bool _isMax;
		private string _amount;
		private bool _ignoreAmountChanges;
		private int _fee;
		private string _password;
		private string _address;
		private bool _isBusy;
		private const string BuildTransactionButtonTextString = "Build Transaction";
		private const string BuildingTransactionButtonTextString = "Building Transaction";

		public SendTabViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel)
		{
			CoinList = new CoinListViewModel(Global.WalletService.Coins);

			BuildTransactionButtonText = BuildTransactionButtonTextString;

			this.WhenAnyValue(x => x.Amount).Subscribe(_ =>
			{
				if (!_ignoreAmountChanges)
				{
					IsMax = false;
				}
			});

			BuildTransactionCommand = ReactiveCommand.Create(async () =>
			{
				IsBusy = true;
				try
				{
					await Task.Delay(5000);

					//CoinList.SelectedCoins
					//IsMax = use all selected coins.
					// backend stuff here.
				}
				catch
				{
				}
				finally
				{
					IsBusy = false;
				}
			},
			this.WhenAny(x => x.IsMax, x => x.Amount, x => x.Address, x => x.IsBusy,
				(isMax, amount, address, busy) => (isMax.Value || !string.IsNullOrWhiteSpace(amount.Value) && !string.IsNullOrWhiteSpace(Address) && !IsBusy)));

			MaxCommand = ReactiveCommand.Create(() =>
			{
				SetMax();
			});

			this.WhenAnyValue(x => x.IsBusy).Subscribe(busy =>
			{
				if (busy)
				{
					BuildTransactionButtonText = BuildingTransactionButtonTextString;
				}
				else
				{
					BuildTransactionButtonText = BuildTransactionButtonTextString;
				}
			});
		}

		private void SetMax()
		{
			IsMax = true;

			_ignoreAmountChanges = true;
			Amount = "All Selected Coins!";
			_ignoreAmountChanges = false;
		}

		public CoinListViewModel CoinList
		{
			get { return _coinList; }
			set { this.RaiseAndSetIfChanged(ref _coinList, value); }
		}

		public bool IsBusy
		{
			get { return _isBusy; }
			set { this.RaiseAndSetIfChanged(ref _isBusy, value); }
		}

		public string BuildTransactionButtonText
		{
			get { return _buildTransactionButtonText; }
			set { this.RaiseAndSetIfChanged(ref _buildTransactionButtonText, value); }
		}

		public bool IsMax
		{
			get { return _isMax; }
			set { this.RaiseAndSetIfChanged(ref _isMax, value); }
		}

		public string Amount
		{
			get { return _amount; }
			set { this.RaiseAndSetIfChanged(ref _amount, value); }
		}

		public int Fee
		{
			get { return _fee; }
			set { this.RaiseAndSetIfChanged(ref _fee, value); }
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public string Address
		{
			get { return _address; }
			set { this.RaiseAndSetIfChanged(ref _address, value); }
		}

		public ReactiveCommand BuildTransactionCommand { get; }

		public ReactiveCommand MaxCommand { get; }
	}
}
