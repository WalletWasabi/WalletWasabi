using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<WalletActionViewModel> _actions;

		private string _title;

		public WalletViewModel(WalletService walletService, bool receiveDominant)
			: base(Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath))
		{
			WalletService = walletService;
			var keyManager = WalletService.KeyManager;
			Name = Path.GetFileNameWithoutExtension(keyManager.FilePath);

			SetBalance(Name);

			Actions = new ObservableCollection<WalletActionViewModel>
			{
				new SendTabViewModel(this, walletService.KeyManager.IsWatchOnly),
				new ReceiveTabViewModel(this),
				new CoinJoinTabViewModel(this),
				new HistoryTabViewModel(this),
				new WalletInfoViewModel(this),
				new TransactionBroadcasterViewModel(this),
			};

			Actions?.OfType<SendTabViewModel>()?.FirstOrDefault()?.DisplayActionTab();
			if (receiveDominant)
			{
				Actions?.OfType<CoinJoinTabViewModel>()?.FirstOrDefault()?.DisplayActionTab();
				Actions?.OfType<HistoryTabViewModel>()?.FirstOrDefault()?.DisplayActionTab();
				Actions?.OfType<ReceiveTabViewModel>()?.FirstOrDefault()?.DisplayActionTab(); // So receive should be shown to the user.
			}
			else
			{
				Actions?.OfType<ReceiveTabViewModel>()?.FirstOrDefault()?.DisplayActionTab();
				Actions?.OfType<CoinJoinTabViewModel>()?.FirstOrDefault()?.DisplayActionTab();
				Actions?.OfType<HistoryTabViewModel>()?.FirstOrDefault()?.DisplayActionTab(); // So history should be shown to the user.
			}

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				Global.UiConfig.LurkingWifeMode = !Global.UiConfig.LurkingWifeMode;
				await Global.UiConfig.ToFileAsync();
			});
		}

		public void OnWalletOpened()
		{
			if (Disposables != null)
			{
				throw new Exception("Wallet opened before it was closed.");
			}

			Disposables = new CompositeDisposable();

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed)))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o => SetBalance(Name))
				.DisposeWith(Disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(x =>
			{
				SetBalance(Name);
			}).DisposeWith(Disposables);
		}

		public void OnWalletClosed()
		{
			Disposables?.Dispose();
		}

		public string Name { get; }

		public WalletService WalletService { get; }

		public override string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		private void SetBalance(string walletName)
		{
			Money balance = Enumerable.Where(WalletService.Coins, c => c.Unspent && !c.IsDust && !c.SpentAccordingToBackend).Sum(c => (long?)c.Amount) ?? 0;

			Title = $"{walletName} ({(Global.UiConfig.LurkingWifeMode.Value ? "#########" : balance.ToString(false, true))} BTC)";
		}
	}
}
