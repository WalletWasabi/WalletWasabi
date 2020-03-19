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
using WalletWasabi.Logging;
using Splat;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<WasabiDocumentTabViewModel> _actions;

		private bool _isExpanded;

		private string _title;

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

		public WalletViewModel(Wallet wallet, bool receiveDominant)
		{
			var global = Locator.Current.GetService<Global>();
			Wallet = wallet;

			Title = Path.GetFileNameWithoutExtension(Wallet.KeyManager.FilePath);

			var keyManager = Wallet.KeyManager;
			Name = Path.GetFileNameWithoutExtension(keyManager.FilePath);

			Actions = new ObservableCollection<WasabiDocumentTabViewModel>();

			SendTabViewModel sendTab = null;
			// If hardware wallet then we need the Send tab.
			if (Wallet?.KeyManager?.IsHardwareWallet is true)
			{
				sendTab = new SendTabViewModel(Wallet);
				Actions.Add(sendTab);
			}
			// If not hardware wallet, but neither watch only then we also need the send tab.
			else if (Wallet?.KeyManager?.IsWatchOnly is false)
			{
				sendTab = new SendTabViewModel(Wallet);
				Actions.Add(sendTab);
			}

			var receiveTab = new ReceiveTabViewModel(Wallet);
			var coinjoinTab = new CoinJoinTabViewModel(Wallet);
			var historyTab = new HistoryTabViewModel(Wallet);

			var advancedAction = new WalletAdvancedViewModel(Wallet);
			var infoTab = new WalletInfoViewModel(Wallet);
			var buildTab = new BuildTabViewModel(Wallet);

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
				WasabiDocumentTabViewModel tabToOpen = global.UiConfig.LastActiveTab switch
				{
					nameof(SendTabViewModel) => sendTab,
					nameof(ReceiveTabViewModel) => receiveTab,
					nameof(CoinJoinTabViewModel) => coinjoinTab,
					nameof(BuildTabViewModel) => buildTab,
					_ => historyTab
				};

				tabToOpen?.DisplayActionTab();
			}

			LurkingWifeModeCommand = ReactiveCommand.Create(() =>
		   {
			   global.UiConfig.LurkingWifeMode = !global.UiConfig.LurkingWifeMode;
			   global.UiConfig.ToFile();
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
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						Money balance = Wallet.Coins.TotalAmount();
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

		public Wallet Wallet { get; }

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public ObservableCollection<WasabiDocumentTabViewModel> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}
	}
}
