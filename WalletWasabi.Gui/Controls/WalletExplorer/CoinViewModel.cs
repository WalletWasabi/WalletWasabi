using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase
	{
		private bool _confirmed;
		private bool _isSelected;
		private string _amountBtc;
		private string _label;
		private int _privacyLevel;
		private string _history;

		public CoinViewModel(CoinListViewModel owner)
		{
			this.WhenAnyValue(x => x.IsSelected).Subscribe(selected =>
			{
				if (selected)
				{
					if (!owner.SelectedCoins.Contains(this))
					{
						owner.SelectedCoins.Add(this);
					}
				}
				else
				{
					owner.SelectedCoins.Remove(this);
				}
			});
		}

		public bool Confirmed
		{
			get { return _confirmed; }
			set { this.RaiseAndSetIfChanged(ref _confirmed, value); }
		}

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
		}

		public string AmountBtc
		{
			get { return _amountBtc; }
			set { this.RaiseAndSetIfChanged(ref _amountBtc, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public int PrivacyLevel
		{
			get { return _privacyLevel; }
			set { this.RaiseAndSetIfChanged(ref _privacyLevel, value); }
		}

		public string History
		{
			get { return _history; }
			set { this.RaiseAndSetIfChanged(ref _history, value); }
		}
	}
}
