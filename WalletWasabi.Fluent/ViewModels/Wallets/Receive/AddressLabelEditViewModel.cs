using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(Title = "Edit Labels")]
	public partial class AddressLabelEditViewModel : RoutableViewModel
	{
		[AutoNotify] private string _label;
		[AutoNotify] private ObservableCollection<string> _labels;

		public AddressLabelEditViewModel(HdPubKey hdPubKey, KeyManager keyManager)
		{
			_labels = new(hdPubKey.Label);
			_label = "";

			NextCommand = ReactiveCommand.Create(() =>
			{
				hdPubKey = keyManager.GetKeys(x => x == hdPubKey).FirstOrDefault();

				hdPubKey?.SetLabel(new SmartLabel(Label), kmToFile: keyManager);

				// hdPubKey.SetLabel(new SmartLabel(Label), kmToFile: keyManager);

				Navigate().Back();
			});
		}
	}
}
