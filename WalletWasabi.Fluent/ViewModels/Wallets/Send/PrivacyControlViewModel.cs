using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;

		[AutoNotify] private ObservableCollection<PocketViewModel> _pockets;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfoInfo)
		{
			_wallet = wallet;
			_transactionInfo = transactionInfoInfo;
			_pockets = new ObservableCollection<PocketViewModel>();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue());

			foreach (var pocket in pockets)
			{
				_pockets.Add(new PocketViewModel
				{
					Labels = string.Join(", ", pocket.Labels),
					TotalBtc = pocket.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC)
				});
			}
		}
	}
}
