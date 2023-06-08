using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(
	Title = "Receive",
	Caption = "Displays wallet receive dialog",
	IconName = "wallet_action_receive",
	Order = 6,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Receive", "Action", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ReceiveViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	private ReceiveViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		SuggestionLabels = new SuggestionLabelsViewModel(wallet, Intent.Receive, 3);

		var nextCommandCanExecute =
			SuggestionLabels
				.WhenAnyValue(x => x.Labels.Count).ToSignal()
				.Merge(SuggestionLabels.WhenAnyValue(x => x.IsCurrentTextValid).ToSignal())
				.Select(_ => SuggestionLabels.Labels.Count > 0 || SuggestionLabels.IsCurrentTextValid);

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);

		ShowExistingAddressesCommand = ReactiveCommand.Create(OnShowExistingAddresses);

		HasUnusedAddresses =
			_wallet
				.UnusedAddresses()
				.ToCollection()
				.Select(x => x.Any())
				.StartWith(false);
	}

	public SuggestionLabelsViewModel SuggestionLabels { get; }

	public ICommand ShowExistingAddressesCommand { get; }

	public IObservable<bool> HasUnusedAddresses { get; }

	private void OnNext()
	{
		SuggestionLabels.ForceAdd = true;
		var address = _wallet.GetNextReceiveAddress(SuggestionLabels.Labels);
		SuggestionLabels.Labels.Clear();

		Navigate().To().ReceiveAddress(_wallet, address, Services.UiConfig.Autocopy);
	}

	private void OnShowExistingAddresses()
	{
		UiContext.Navigate(NavigationTarget.DialogScreen).To().ReceiveAddresses(_wallet);
	}
}
