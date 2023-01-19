using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Edit Labels")]
public partial class AddressLabelEditViewModel : RoutableViewModel
{
	[ObservableProperty] private bool _isCurrentTextValid;

	public AddressLabelEditViewModel(ReceiveAddressesViewModel owner, HdPubKey hdPubKey, KeyManager keyManager)
	{
		SuggestionLabels = new SuggestionLabelsViewModel(keyManager, Intent.Receive, 3, hdPubKey.Label);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = new RelayCommand(
			() =>
			{
				hdPubKey.SetLabel(new SmartLabel(SuggestionLabels.Labels), kmToFile: keyManager);
				owner.InitializeAddresses();
				Navigate().Back();
			},
			() => SuggestionLabels.Labels.Count > 0 || IsCurrentTextValid);

			this.WhenAnyValue(x => x.SuggestionLabels.Labels.Count, x => x.IsCurrentTextValid)
				.Subscribe(_ => NextCommand.NotifyCanExecuteChanged());
	}

	public SuggestionLabelsViewModel SuggestionLabels { get; }
}
