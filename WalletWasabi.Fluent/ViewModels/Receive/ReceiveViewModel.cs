using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveViewModel : NavBarItemViewModel
	{
		[AutoNotify] private string _reference;
		[AutoNotify] private HashSet<string> _suggestions;

		public ReceiveViewModel(WalletManager walletManager, Wallet wallet)
		{
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
				if (minGapLimitIncreased)
				{
					int minGapLimit = wallet.KeyManager.MinGapLimit.Value;
					int prevMinGapLimit = minGapLimit - 1;
					//TODO: Notify the user
					// NotificationHelpers.Warning($"{nameof(KeyManager.MinGapLimit)} increased from {prevMinGapLimit} to {minGapLimit}.");
				}

				Navigate().To(new ReceiveAddressesViewModel(newKey));
			},
			nextCommandCanExecute);
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