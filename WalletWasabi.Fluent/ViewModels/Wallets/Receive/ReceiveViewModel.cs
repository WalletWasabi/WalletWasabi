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
		private readonly Wallet _wallet;
		[AutoNotify] private ObservableCollection<string> _labels;
		[AutoNotify] private HashSet<string> _suggestions;
		[AutoNotify] private bool _isExistingAddressesButtonVisible;

		public ReceiveViewModel(Wallet wallet) : base(NavigationMode.Normal)
		{
			_wallet = wallet;
			_labels = new ObservableCollection<string>();
			_suggestions = GetLabels();

			SelectionMode = NavBarItemSelectionMode.Button;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			EnableBack = false;

			NextCommand = ReactiveCommand.Create(OnNext, _labels.ToObservableChangeSet()
				.Select(_ => _labels.Count > 0));

			ShowExistingAddressesCommand = ReactiveCommand.Create(OnShowExistingAddresses);
		}

		private void OnNext()
		{
			var newKey = _wallet.KeyManager.GetNextReceiveKey(new SmartLabel(Labels), out bool minGapLimitIncreased);

			if (minGapLimitIncreased)
			{
				int minGapLimit = _wallet.KeyManager.MinGapLimit.Value;
				int prevMinGapLimit = minGapLimit - 1;
				var minGapLimitMessage = $"Minimum gap limit increased from {prevMinGapLimit} to {minGapLimit}.";
				// TODO: notification
			}

			Labels.Clear();

			Navigate().To(new ReceiveAddressViewModel(_wallet, newKey));
		}

		private void OnShowExistingAddresses()
		{
			Navigate().To(new ReceiveAddressesViewModel(_wallet, Suggestions));
		}

		public ICommand ShowExistingAddressesCommand { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(isInHistory, disposable);

			IsExistingAddressesButtonVisible = _wallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Any();
		}

		private HashSet<string> GetLabels()
		{
			// Don't refresh wallet list as it may be slow.
			IEnumerable<SmartLabel> labels = Services.WalletManager.GetWallets(refreshWalletList: false)
				.Select(x => x.KeyManager)
				.SelectMany(x => x.GetLabels());

			var txStore = Services.BitcoinStore.TransactionStore;
			if (txStore is { })
			{
				labels = labels.Concat(txStore.GetLabels());
			}

			return labels.SelectMany(x => x.Labels).ToHashSet();
		}
	}
}
