using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions.Receive
{
	[NavigationMetaData(
		Title = "Receive",
		Caption = "",
		IconName = "wallet_action_receive",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class ReceiveWalletActionViewModel : WalletActionViewModel
	{
		[AutoNotify] private string _reference;
		[AutoNotify] private HashSet<string> _suggestions;
		[AutoNotify] private bool _isExistingAddressesButtonVisible;

		public ReceiveWalletActionViewModel(WalletViewModelBase wallet, WalletManager walletManager, BitcoinStore bitcoinStore) : base(wallet)
		{
			Title = "Receive";
			SelectionMode = NavBarItemSelectionMode.Button;
			WasabiWallet = wallet.Wallet;
			_reference = "";
			_suggestions = GetLabels(walletManager, bitcoinStore);

			var nextCommandCanExecute =
				this.WhenAnyValue(x => x.Reference)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Select(reference => !string.IsNullOrEmpty(reference));

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					var newKey = WasabiWallet.KeyManager.GetNextReceiveKey(Reference, out bool minGapLimitIncreased);

					string? minGapLimitMessage;
					if (minGapLimitIncreased || true)
					{
						int minGapLimit = WasabiWallet.KeyManager.MinGapLimit.Value;
						int prevMinGapLimit = minGapLimit - 1;
						minGapLimitMessage = $"Minimum gap limit increased from {prevMinGapLimit} to {minGapLimit}.";
					}

					Navigate().To(new ReceiveAddressViewModel(newKey, WasabiWallet.Network, WasabiWallet.KeyManager.MasterFingerprint, WasabiWallet.KeyManager.IsHardwareWallet, minGapLimitMessage));
				},
				nextCommandCanExecute);

			ShowExistingAddressesCommand = ReactiveCommand.Create(() => Navigate().To(new ReceiveAddressesViewModel(WasabiWallet)));
		}

		public Wallet WasabiWallet { get; }

		public ICommand ShowExistingAddressesCommand { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			IsExistingAddressesButtonVisible = WasabiWallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Any();
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