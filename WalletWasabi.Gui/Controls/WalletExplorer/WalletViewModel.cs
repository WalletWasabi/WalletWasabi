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
using WalletWasabi.Logging;
using Splat;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WalletViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<WalletActionViewModel> _actions;
		private bool _isExpanded;

		public WalletViewModel(bool receiveDominant, WalletService walletService)
		{
			var global = Locator.Current.GetService<Global>();
			WalletService = walletService;

			Title = Path.GetFileNameWithoutExtension(WalletService.KeyManager.FilePath);
			
			var keyManager = WalletService.KeyManager;
			Name = Path.GetFileNameWithoutExtension(keyManager.FilePath);

			Actions = new ObservableCollection<WalletActionViewModel>();

			SendTabViewModel sendTab = null;
			// If hardware wallet then we need the Send tab.
			if (WalletService?.KeyManager?.IsHardwareWallet is true)
			{
				sendTab = new SendTabViewModel(this);
				Actions.Add(sendTab);
			}
			// If not hardware wallet, but neither watch only then we also need the send tab.
			else if (WalletService?.KeyManager?.IsWatchOnly is false)
			{
				sendTab = new SendTabViewModel(this);
				Actions.Add(sendTab);
			}

			ReceiveTabViewModel receiveTab = new ReceiveTabViewModel(this);
			CoinJoinTabViewModel coinjoinTab = new CoinJoinTabViewModel(this);
			HistoryTabViewModel historyTab = new HistoryTabViewModel(this);

			var advancedAction = new WalletAdvancedViewModel(this);
			WalletInfoViewModel infoTab = new WalletInfoViewModel(this);
			SendTabViewModel buildTab = new SendTabViewModel(this, isTransactionBuilder: true);

			Actions.Add(receiveTab);
			Actions.Add(coinjoinTab);
			Actions.Add(historyTab);

			Actions.Add(advancedAction);
			advancedAction.Items.Add(infoTab);
			advancedAction.Items.Add(buildTab);

			// Open and select tabs.
			sendTab?.DisplayActionTab();
			if (receiveDominant)
			{
				coinjoinTab.DisplayActionTab();
				historyTab.DisplayActionTab();
				receiveTab.DisplayActionTab(); // So receive should be shown to the user.
			}
			else
			{
				receiveTab.DisplayActionTab();
				coinjoinTab.DisplayActionTab();
				historyTab.DisplayActionTab(); // So history should be shown to the user.
			}

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var global = Locator.Current.GetService<Global>();

				global.UiConfig.LurkingWifeMode = !global.UiConfig.LurkingWifeMode;
				await global.UiConfig.ToFileAsync();
			});
		}

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public Guid Id { get; set; } = Guid.NewGuid();

		public void OnWalletOpened()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			var global = Locator.Current.GetService<Global>();

			Observable.Merge(
				Observable.FromEventPattern(WalletService.TransactionProcessor, nameof(WalletService.TransactionProcessor.CoinReceived)).Select(_ => Unit.Default),
				Observable.FromEventPattern(WalletService.TransactionProcessor, nameof(WalletService.TransactionProcessor.CoinSpent)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.5))
				.Merge(global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						Money balance = WalletService.Coins.TotalAmount();
						Title = $"{Name} ({(global.UiConfig.LurkingWifeMode ? "#########" : balance.ToString(false, true))} BTC)";
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			IsExpanded = true;
		}

		public void OnWalletClosed()
		{
			Disposables?.Dispose();
		}

		public string Name { get; }

		public WalletService WalletService { get; }

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}
	}
}
