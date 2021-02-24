using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
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

			var clusters = _wallet.Coins.OfType<SmartCoin>().GroupBy(x=>x.HdPubKey.Cluster);

			foreach (var cluster in clusters)
			{
				var labels = cluster.Key.Labels;

				var amount = cluster.Select(x => x.Amount).Sum(x => x.ToDecimal(MoneyUnit.BTC));

				_pockets.Add(new PocketViewModel
				{
					Labels = string.Join(", ", labels.Labels),
					TotalBtc = amount
				});
			}
		}
	}
}