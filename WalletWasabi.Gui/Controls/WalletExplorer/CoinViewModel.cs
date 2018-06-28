using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using WalletWasabi.Models;
using NBitcoin;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase
	{
		private bool _isSelected;
		private SmartCoin _model;
		private int _privacyLevel;
		private string _history;
		private CoinListViewModelBase _owner;

		public CoinViewModel(CoinListViewModelBase owner, SmartCoin model)
		{
			_model = model;
			_owner = owner;

			this.WhenAnyValue(x => x.IsSelected).Subscribe(selected =>
			{
				if (selected)
				{
					if (!_owner.SelectedCoins.Contains(this))
					{
						_owner.SelectedCoins.Add(this);
					}
				}
				else
				{
					_owner.SelectedCoins.Remove(this);
				}
			});

			model.WhenAnyValue(x => x.Confirmed).ObserveOn(RxApp.MainThreadScheduler).Subscribe(confirmed =>
			{
				this.RaisePropertyChanged(nameof(Confirmed));
			});

			History = "Calculate History";
		}

		public void ChangeOwner(CoinListViewModelBase newOwner)
		{
			var selected = IsSelected;

			IsSelected = false;

			_owner = newOwner;

			IsSelected = selected;
		}

		public bool Confirmed => _model.Confirmed;

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
		}

		public Money Amount => _model.Amount;

		public string AmountBtc => _model.Amount.ToString(false, true);

		public string Label => _model.Label;

		public int Height => _model.Height;

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
