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

		public void OpenWallet(bool receiveDominant)
		{
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

			var receiveTab = new ReceiveTabViewModel(Wallet);
			var coinjoinTab = new CoinJoinTabViewModel(Wallet);
			var historyTab = new HistoryTabViewModel(Wallet);

			var advancedAction = new WalletAdvancedViewModel();
			var infoTab = new WalletInfoViewModel(Wallet);
			var buildTab = new BuildTabViewModel(Wallet);

			Actions.Add(receiveTab);
			Actions.Add(coinjoinTab);
			Actions.Add(historyTab);

			Actions.Add(advancedAction);
			advancedAction.Items.Add(infoTab);
			advancedAction.Items.Add(buildTab);

			var shell = IoC.Get<IShell>();

			if (sendTab is { })
			{
				shell.AddOrSelectDocument(sendTab);
			}

			shell.AddOrSelectDocument(receiveTab);			
			shell.AddOrSelectDocument(coinjoinTab);
			shell.AddOrSelectDocument(historyTab);			

			// Select tab
			if (receiveDominant)
			{
				shell.AddOrSelectDocument(receiveTab);
			}
			else
			{
				WasabiDocumentTabViewModel tabToOpen = Global.UiConfig.LastActiveTab switch
				{
					nameof(SendTabViewModel) => sendTab,
					nameof(ReceiveTabViewModel) => receiveTab,
					nameof(CoinJoinTabViewModel) => coinjoinTab,
					nameof(BuildTabViewModel) => buildTab,
					_ => historyTab
				};

				if (tabToOpen is { })
				{
					shell.AddOrSelectDocument(tabToOpen);
				}
			}

			IsExpanded = true;
		}
	}
}
