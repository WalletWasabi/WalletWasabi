using System.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Recipient")]
public partial class LabelEntryDialogViewModel : DialogViewModelBase<SmartLabel?>
{
	public LabelEntryDialogViewModel(Wallet wallet, SmartLabel label)
	{
		SuggestionLabels = new SuggestionLabelsViewModel(wallet.KeyManager, Intent.Send, 3)
		{
			Labels = { label.Labels }
		};

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = ReactiveCommand.Create(OnNext, SuggestionLabels.IsValid);
	}

	public SuggestionLabelsViewModel SuggestionLabels { get; }

	private void OnNext()
	{
		Close(DialogResultKind.Normal, new SmartLabel(SuggestionLabels.Labels.ToArray()));
	}
}
