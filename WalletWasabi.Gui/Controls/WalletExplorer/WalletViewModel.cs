using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WalletViewModelBase
	{
		private ObservableCollection<ViewModelBase> _actions;
		private SendTabViewModel _sendTab;
		private ReceiveTabViewModel _receiveTab;
		private CoinJoinTabViewModel _coinjoinTab;
		private HistoryTabViewModel _historyTab;
		private WalletInfoViewModel _infoTab;
		private BuildTabViewModel _buildTab;

		public WalletViewModel(Wallet wallet) : base(wallet)
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");
			Actions = new ObservableCollection<ViewModelBase>();
			Global = Locator.Current.GetService<Global>();

			LurkingWifeModeCommand = ReactiveCommand.Create(() =>
			{
				Global.UiConfig.LurkingWifeMode = !Global.UiConfig.LurkingWifeMode;
				Global.UiConfig.ToFile();
			});

			LurkingWifeModeCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						Money balance = Wallet.Coins.TotalAmount();
						Title = $"{WalletName} ({(Global.UiConfig.LurkingWifeMode ? "#########" : balance.ToString(false, true))} BTC)";
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			SendTabViewModel sendTab = null;
			// If hardware wallet then we need the Send tab.
			if (KeyManager.IsHardwareWallet is true)
			{
				sendTab = new SendTabViewModel(Wallet);
				Actions.Add(sendTab);
			}
			// If not hardware wallet, but neither watch only then we also need the send tab.
			else if (KeyManager.IsWatchOnly is false)
			{
				sendTab = new SendTabViewModel(Wallet);
				Actions.Add(sendTab);
			}

			_receiveTab = new ReceiveTabViewModel(Wallet);
			_coinjoinTab = new CoinJoinTabViewModel(Wallet);
			_historyTab = new HistoryTabViewModel(Wallet);

			var advancedAction = new WalletAdvancedViewModel();
			_infoTab = new WalletInfoViewModel(Wallet);
			_buildTab = new BuildTabViewModel(Wallet);

			Actions.Add(_receiveTab);
			Actions.Add(_coinjoinTab);
			Actions.Add(_historyTab);

			Actions.Add(advancedAction);
			advancedAction.Items.Add(_infoTab);
			advancedAction.Items.Add(_buildTab);
		}

		public Global Global { get; }
		public KeyManager KeyManager => Wallet.KeyManager;
		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public ObservableCollection<ViewModelBase> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		private CompositeDisposable Disposables { get; set; }

		public void OpenWallet(bool receiveDominant, bool firstWalletOpened)
		{
			IsExpanded = true;

			if (!firstWalletOpened)
			{
				return;
			}

			var shell = IoC.Get<IShell>();

			if (_sendTab is { })
			{
				shell.AddOrSelectDocument(_sendTab);
			}

			shell.AddOrSelectDocument(_receiveTab);
			shell.AddOrSelectDocument(_coinjoinTab);
			shell.AddOrSelectDocument(_historyTab);

			// Select tab
			if (receiveDominant)
			{
				shell.AddOrSelectDocument(_receiveTab);
			}
			else
			{
				WasabiDocumentTabViewModel tabToOpen = Global.UiConfig.LastActiveTab switch
				{
					nameof(SendTabViewModel) => _sendTab,
					nameof(ReceiveTabViewModel) => _receiveTab,
					nameof(CoinJoinTabViewModel) => _coinjoinTab,
					nameof(BuildTabViewModel) => _buildTab,
					_ => _historyTab
				};

				if (tabToOpen is { })
				{
					shell.AddOrSelectDocument(tabToOpen);
				}
			}
		}
	}
}
