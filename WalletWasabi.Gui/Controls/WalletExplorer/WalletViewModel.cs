using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Services;
using WalletWasabi.Logging;
using Splat;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WalletViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<WalletActionViewModel> _actions;

		public Guid Id { get; set; } = Guid.NewGuid();

		public WalletViewModel(Wallet wallet, bool receiveDominant) : base(wallet)
		{
			Title = Path.GetFileNameWithoutExtension(wallet.KeyManager.FilePath);								

			Actions = new ObservableCollection<WalletActionViewModel>();

			SendTabViewModel sendTab = null;
			// If hardware wallet then we need the Send tab.
			if (wallet?.KeyManager?.IsHardwareWallet is true)
			{
				sendTab = new SendTabViewModel(this);
				Actions.Add(sendTab);
			}
			// If not hardware wallet, but neither watch only then we also need the send tab.
			else if (wallet?.KeyManager?.IsWatchOnly is false)
			{
				sendTab = new SendTabViewModel(this);
				Actions.Add(sendTab);
			}

			var receiveTab = new ReceiveTabViewModel(this);
			var coinjoinTab = new CoinJoinTabViewModel(this);
			var historyTab = new HistoryTabViewModel(this);

			var advancedAction = new WalletAdvancedViewModel(this);
			var infoTab = new WalletInfoViewModel(this);
			var buildTab = new BuildTabViewModel(this);

			Actions.Add(receiveTab);
			Actions.Add(coinjoinTab);
			Actions.Add(historyTab);

			Actions.Add(advancedAction);
			advancedAction.Items.Add(infoTab);
			advancedAction.Items.Add(buildTab);

			// Open tabs.
			sendTab?.DisplayActionTab();
			receiveTab.DisplayActionTab();
			coinjoinTab.DisplayActionTab();
			historyTab.DisplayActionTab();

			// Select tab
			if (receiveDominant)
			{
				receiveTab.DisplayActionTab();
			}
			else
			{
				var global = Locator.Current.GetService<Global>();

				WalletActionViewModel tabToOpen = global.UiConfig.LastActiveTab switch
				{
					nameof(SendTabViewModel) => sendTab,
					nameof(ReceiveTabViewModel) => receiveTab,
					nameof(CoinJoinTabViewModel) => coinjoinTab,
					nameof(BuildTabViewModel) => buildTab,
					_ => historyTab
				};

				tabToOpen?.DisplayActionTab();
			}

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var global = Locator.Current.GetService<Global>();

				global.UiConfig.LurkingWifeMode = !global.UiConfig.LurkingWifeMode;
				await global.UiConfig.ToFileAsync();
			});

			LurkingWifeModeCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public void OnWalletOpened()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			var global = Locator.Current.GetService<Global>();

			Observable.Merge(
				Observable.FromEventPattern(Wallet.WalletService.TransactionProcessor, nameof(Wallet.WalletService.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						Money balance = Wallet.WalletService.Coins.TotalAmount();
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

		public string Name => Title; // TODO remove

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}
	}
}
