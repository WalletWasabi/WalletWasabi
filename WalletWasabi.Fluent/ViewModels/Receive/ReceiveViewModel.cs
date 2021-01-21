using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveViewModel : NavBarItemViewModel
	{
		public Wallet Wallet { get; }
		[AutoNotify] private string _reference;
		[AutoNotify] private HashSet<string> _suggestions;
		[AutoNotify] private bool _isExistingAddressesButtonVisible;

		public ReceiveViewModel(WalletManager walletManager, Wallet wallet)
		{
			Wallet = wallet;
			Title = "Receive";
			_reference = "";
			_suggestions = GetLabels(walletManager);

			var nextCommandCanExecute =
				this.WhenAnyValue(x => x.Reference)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Select(reference => !string.IsNullOrEmpty(reference));

			NextCommand = ReactiveCommand.Create(
			() =>
			{
				var newKey = wallet.KeyManager.GetNextReceiveKey(Reference, out bool minGapLimitIncreased);

				string? minGapLimitMessage = default;
				if (minGapLimitIncreased || true)
				{
					int minGapLimit = wallet.KeyManager.MinGapLimit.Value;
					int prevMinGapLimit = minGapLimit - 1;
					minGapLimitMessage = $"Minimum gap limit increased from {prevMinGapLimit} to {minGapLimit}.";
				}

				Navigate().To(new ReceiveAddressViewModel(newKey, wallet.Network, wallet.KeyManager.MasterFingerprint, wallet.KeyManager.IsHardwareWallet, minGapLimitMessage));
			},
			nextCommandCanExecute);

			ShowExistingAddressesCommand = ReactiveCommand.Create(() => Navigate().To(new ReceiveAddressesViewModel(wallet)));
		}

		public ICommand ShowExistingAddressesCommand { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			IsExistingAddressesButtonVisible = Wallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Any();
		}

		private HashSet<string> GetLabels(WalletManager walletManager)
		{
			//TODO: remove locator
			var global = Locator.Current.GetService<Global>();
			var bitcoinStore = global.BitcoinStore;

			// Don't refresh wallet list as it may be slow.
			IEnumerable<SmartLabel> labels = walletManager.GetWallets(refreshWalletList: false)
				.Select(x => x.KeyManager)
				.SelectMany(x => x.GetLabels());

			var txStore = bitcoinStore.TransactionStore;
			if (txStore is { })
			{
				labels = labels.Concat(txStore.GetLabels());
			}

			return labels.SelectMany(x => x.Labels).ToHashSet();
		}
	}
}