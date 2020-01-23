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
	public class WalletViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<WalletActionViewModel> _actions;

		private bool _isExpanded;

		private string _title;

		public Guid Id { get; set; } = Guid.NewGuid();

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public WalletViewModel(bool receiveDominant)
		{
			var global = Locator.Current.GetService<Global>();

			Title = Path.GetFileNameWithoutExtension(global.WalletService.KeyManager.FilePath);

			WalletService = global.WalletService;
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
			if (receiveDominant || global.UiConfig.LastActiveTab == receiveTab.GetType().Name)
			{
				receiveTab.DisplayActionTab(); // So receive should be shown to the user.
			}
			else if (global.UiConfig.LastActiveTab == sendTab?.GetType().Name)
			{
				sendTab.DisplayActionTab(); // So send should be shown to the user.
			}
			else if (global.UiConfig.LastActiveTab == buildTab.GetType().Name)
			{
				buildTab.DisplayActionTab(); // So build should be shown to the user.
			}
			else if (global.UiConfig.LastActiveTab == historyTab.GetType().Name)
			{
				historyTab.DisplayActionTab(); // So history should be shown to the user.
			}
			else
			{
				coinjoinTab.DisplayActionTab(); // So coinjoin should be shown to the user.
			}

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
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
				Observable.FromEventPattern(global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
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
