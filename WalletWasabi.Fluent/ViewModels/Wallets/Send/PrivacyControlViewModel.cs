using System.Collections.ObjectModel;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : RoutableViewModel
	{
		[AutoNotify] private ObservableCollection<PocketViewModel> _pockets;

		public PrivacyControlViewModel()
		{
			_pockets = new ObservableCollection<PocketViewModel>();

			_pockets.Add(new PocketViewModel
			{
				TotalBtc = 1.3m,
				Labels = "Adam, Max, Dan"
			});

			_pockets.Add(new PocketViewModel
			{
				TotalBtc = 2.4m,
				Labels = "Bob, Alice"
			});

			_pockets.Add(new PocketViewModel
			{
				TotalBtc = 0.3m,
				Labels = "Coinbase"
			});

			_pockets.Add(new PocketViewModel
			{
				TotalBtc = 0.1m,
				Labels = "David"
			});

			_pockets.Add(new PocketViewModel
			{
				TotalBtc = 0.1m,
				Labels = "Unlabelled"
			});

			_pockets.Add(new PocketViewModel
			{
				TotalBtc = 0.3m,
				Labels = "Private"
			});
		}
	}
}