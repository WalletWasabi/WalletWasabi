using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "wallet_action_receive",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class ReceiveViewModel : NavBarItemViewModel
	{
		[AutoNotify] private ObservableCollection<string> _labels;
		[AutoNotify] private HashSet<string> _suggestions;
		[AutoNotify] private bool _isExistingAddressesButtonVisible;

		public ReceiveViewModel(WalletViewModelBase wallet, WalletManager walletManager, BitcoinStore bitcoinStore) : base(NavigationMode.Normal)
		{
			WasabiWallet = wallet.Wallet;
			_labels = new ObservableCollection<string>();
			_suggestions = GetLabels(walletManager, bitcoinStore);

			SelectionMode = NavBarItemSelectionMode.Button;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			EnableBack = false;

			NextCommand = ReactiveCommand.Create(OnNext, _labels.ToObservableChangeSet()
				.Select(_ => _labels.Count > 0));

			ShowExistingAddressesCommand = ReactiveCommand.Create(OnShowExistingAddresses);
		}

		private void OnNext()
		{
			var newKey = WasabiWallet.KeyManager.GetNextReceiveKey(new SmartLabel(Labels), out bool minGapLimitIncreased);

			if (minGapLimitIncreased)
			{
				int minGapLimit = WasabiWallet.KeyManager.MinGapLimit.Value;
				int prevMinGapLimit = minGapLimit - 1;
				var minGapLimitMessage = $"Minimum gap limit increased from {prevMinGapLimit} to {minGapLimit}.";
				// TODO: notification
			}

			Navigate().To(new ReceiveAddressViewModel(newKey, WasabiWallet.Network, WasabiWallet.KeyManager.MasterFingerprint,
				WasabiWallet.KeyManager.IsHardwareWallet));
		}

		private void OnShowExistingAddresses()
		{
			Navigate().To(new ReceiveAddressesViewModel(WasabiWallet));
		}

		public Wallet WasabiWallet { get; }

		public ICommand ShowExistingAddressesCommand { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(isInHistory, disposable);

			IsExistingAddressesButtonVisible = WasabiWallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Any();
			Labels.Clear();
		}

		private HashSet<string> GetLabels(WalletManager walletManager, BitcoinStore store)
		{
			// Don't refresh wallet list as it may be slow.
			IEnumerable<SmartLabel> labels = walletManager.GetWallets(refreshWalletList: false)
				.Select(x => x.KeyManager)
				.SelectMany(x => x.GetLabels());

			var txStore = store.TransactionStore;
			if (txStore is { })
			{
				labels = labels.Concat(txStore.GetLabels());
			}

			return labels.SelectMany(x => x.Labels).ToHashSet();
		}
	}
}
