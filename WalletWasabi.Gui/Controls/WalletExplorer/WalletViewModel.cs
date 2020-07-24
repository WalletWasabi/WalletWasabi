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
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WalletViewModelBase
	{
		private ObservableCollection<ViewModelBase> _actions;

		protected WalletViewModel(Wallet wallet) : base(wallet)
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Actions = new ObservableCollection<ViewModelBase>();

			UiConfig = Locator.Current.GetService<Global>().UiConfig;

			WalletManager = Locator.Current.GetService<Global>().WalletManager;

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						Money balance = Wallet.Coins.TotalAmount();
						Title = $"{WalletName} ({(UiConfig.LurkingWifeMode ? "#########" : balance.ToString(false, true))} BTC)";

						TitleTip = balance.ToUsdString(Wallet.Synchronizer.UsdExchangeRate, UiConfig.LurkingWifeMode);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			// If hardware wallet or not watch only wallet then we need the Send tab.
			if (Wallet.KeyManager.IsHardwareWallet || !Wallet.KeyManager.IsWatchOnly)
			{
				SendTab = new SendTabViewModel(Wallet);
				Actions.Add(SendTab);
			}

			ReceiveTab = new ReceiveTabViewModel(Wallet);
			HistoryTab = new HistoryTabViewModel(Wallet);

			var advancedAction = new WalletAdvancedViewModel();
			InfoTab = new WalletInfoViewModel(Wallet);
			BuildTab = new BuildTabViewModel(Wallet);

			Actions.Add(ReceiveTab);

			// If not watch only wallet (not hww) then we need the CoinJoin tab.
			if (!Wallet.KeyManager.IsWatchOnly)
			{
				CoinjoinTab = new CoinJoinTabViewModel(Wallet);
				Actions.Add(CoinjoinTab);
			}

			Actions.Add(HistoryTab);

			Actions.Add(advancedAction);
			advancedAction.Items.Add(InfoTab);
			advancedAction.Items.Add(BuildTab);
		}

		public static WalletViewModel Create(Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletViewModel(wallet)
				: wallet.KeyManager.IsWatchOnly
					? new WatchOnlyWalletViewModel(wallet)
					: new WalletViewModel(wallet);
		}

		private SendTabViewModel SendTab { get; set; }

		private ReceiveTabViewModel ReceiveTab { get; set; }

		private CoinJoinTabViewModel CoinjoinTab { get; set; }

		private HistoryTabViewModel HistoryTab { get; set; }

		private WalletInfoViewModel InfoTab { get; set; }

		private BuildTabViewModel BuildTab { get; set; }

		private UiConfig UiConfig { get; }

		private WalletManager WalletManager { get; }

		public ObservableCollection<ViewModelBase> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		private CompositeDisposable Disposables { get; set; }

		public void OpenWalletTabs()
		{
			var shell = IoC.Get<IShell>();

			if (SendTab is { })
			{
				shell.AddOrSelectDocument(SendTab);
			}

			shell.AddOrSelectDocument(ReceiveTab);

			if (CoinjoinTab is { })
			{
				shell.AddOrSelectDocument(CoinjoinTab);
			}

			shell.AddOrSelectDocument(HistoryTab);

			SelectTab(shell);
		}

		private void SelectTab(IShell shell)
		{
			if (Wallet.Coins.Any())
			{
				WasabiDocumentTabViewModel tabToOpen = UiConfig.LastActiveTab switch
				{
					nameof(SendTabViewModel) => SendTab,
					nameof(ReceiveTabViewModel) => ReceiveTab,
					nameof(CoinJoinTabViewModel) => CoinjoinTab,
					nameof(BuildTabViewModel) => BuildTab,
					_ => HistoryTab
				};

				if (tabToOpen is { })
				{
					shell.AddOrSelectDocument(tabToOpen);
				}
			}
			else
			{
				shell.AddOrSelectDocument(ReceiveTab);
			}
		}
	}
}
