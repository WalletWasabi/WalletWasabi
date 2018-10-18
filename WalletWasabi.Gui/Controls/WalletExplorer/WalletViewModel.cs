using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<WalletActionViewModel> _actions;

		private string _title;

		public override string Title
		{
			get { return _title; }
			set { this.RaiseAndSetIfChanged(ref _title, value); }
		}

		public WalletViewModel(string name, bool receiveDominant)
			: base(name)
		{
			var coinsChanged = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged));
			var coinSpent = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed));

			coinsChanged
				.Merge(coinSpent)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
				{
					SetBalance(name);
				});

			SetBalance(name);

			if (receiveDominant)
			{
				_actions = new ObservableCollection<WalletActionViewModel>
				{
					new SendTabViewModel(this),
					new CoinJoinTabViewModel(this),
					new HistoryTabViewModel(this),
					new ReceiveTabViewModel(this)
				};
			}
			else
			{
				_actions = new ObservableCollection<WalletActionViewModel>
				{
					new SendTabViewModel(this),
					new ReceiveTabViewModel(this),
					new CoinJoinTabViewModel(this),
					new HistoryTabViewModel(this)
				};
			}

			foreach (var vm in _actions)
			{
				vm.DisplayActionTab();
			}
		}

		public string Name { get; }

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get { return _actions; }
			set { this.RaiseAndSetIfChanged(ref _actions, value); }
		}

		private void SetBalance(string walletName)
		{
			Money balance = Global.WalletService.CoinsWhereSum(x => x.Unspent, y => y.Amount) ?? 0;
			Title = $"{walletName} ({balance.ToString(false, true)} BTC)";
		}
	}
}
