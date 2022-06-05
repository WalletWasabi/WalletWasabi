using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Edit Labels")]
public partial class AddressLabelEditViewModel : RoutableViewModel
{
	[AutoNotify] private SmartLabel? _finalLabel;
	[AutoNotify] private bool _isCurrentTextValid;

	public AddressLabelEditViewModel(ReceiveAddressesViewModel owner, HdPubKey hdPubKey, KeyManager keyManager)
	{
		SuggestionLabels = new SuggestionLabelsViewModel(3, hdPubKey.Label);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		SuggestionLabels.Labels
			.WhenAnyValue(x => x.Count)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => FinalLabel = new SmartLabel(SuggestionLabels.Labels));

		var canExecute =
			this.WhenAnyValue(x => x.FinalLabel, x => x.IsCurrentTextValid)
				.Select(tup =>
				{
					var (finalLabel, isCurrentTextValid) = tup;
					return finalLabel is { IsEmpty: false } || isCurrentTextValid;
				});

		NextCommand = ReactiveCommand.Create(
			() =>
			{
				if (FinalLabel is null)
				{
					return;
				}

				hdPubKey.SetLabel(FinalLabel, kmToFile: keyManager);
				owner.InitializeAddresses();
				Navigate().Back();
			},
			canExecute);
	}

	public SuggestionLabelsViewModel SuggestionLabels { get; }
}
