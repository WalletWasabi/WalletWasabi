using System.Collections.ObjectModel;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(Title = "Edit Labels")]
	public partial class AddressLabelEditViewModel : RoutableViewModel
	{
		[AutoNotify] private ObservableCollection<string> _labels;

		public AddressLabelEditViewModel(HdPubKey hdPubKey, KeyManager keyManager)
		{
			_labels = new(hdPubKey.Label);

			NextCommand = ReactiveCommand.Create(() =>
			{
				hdPubKey.SetLabel(new SmartLabel(Labels), kmToFile: keyManager);
				Navigate().Back();
			});
		}
	}
}
