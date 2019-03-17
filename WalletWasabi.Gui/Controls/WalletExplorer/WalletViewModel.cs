using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<WalletActionViewModel> _actions;

		private string _title;

		public WalletViewModel(WalletService walletService, bool receiveDominant)
			: base(Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath))
		{
			throw new Exception("TODO");

			// TODO implement unsubscribing from Global.WalletService.Coins.

			WalletService = walletService;
			Name = Path.GetFileNameWithoutExtension(WalletService.KeyManager.FilePath);
			var coinsChanged = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged));
			var coinSpent = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed));

			coinsChanged
				.Merge(coinSpent)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
				{
					SetBalance(Name);
				});

			SetBalance(Name);

			Actions = new ObservableCollection<WalletActionViewModel>
			{
				new SendTabViewModel(this),
				new ReceiveTabViewModel(this),
				new CoinJoinTabViewModel(this),
				new HistoryTabViewModel(this),
				new WalletInfoViewModel(this)
			};

			Actions[0].DisplayActionTab();
			if (receiveDominant)
			{
				Actions[2].DisplayActionTab();
				Actions[3].DisplayActionTab();
				Actions[1].DisplayActionTab();
			}
			else
			{
				Actions[1].DisplayActionTab();
				Actions[2].DisplayActionTab();
				Actions[3].DisplayActionTab();
			}
		}

		public string Name { get; }

		public WalletService WalletService { get; }

		public override string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		private void SetBalance(string walletName)
		{
			Money balance = Enumerable.Where(WalletService.Coins, c => c.Unspent && !c.IsDust && !c.SpentAccordingToBackend).Sum(c => (long?)c.Amount) ?? 0;
			Title = $"{walletName} ({balance.ToString(false, true)} BTC)";
		}
	}
}
