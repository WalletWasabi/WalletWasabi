using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(Title = "Edit Labels")]
	public partial class AddressLabelEditViewModel : RoutableViewModel
	{
		[AutoNotify] private string _label;
		[AutoNotify] private AddressViewModel _targetAddress;
		[AutoNotify] private ObservableCollection<string> _labels;

		public AddressLabelEditViewModel(AddressViewModel addressViewModel)
		{
			_targetAddress = addressViewModel;
			_labels = new();

			foreach (var labelString in _targetAddress.Labels)
			{
				_labels.Add(labelString);
			}

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				_targetAddress.Labels = Labels;
				Navigate().Back();
			});
		}
	}
}