using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "About", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AboutAdvancedInfoViewModel : DialogViewModelBase<System.Reactive.Unit>
{
	public AboutAdvancedInfoViewModel()
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = CancelCommand;
	}

	public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

	public Version HwiVersion => Constants.HwiVersion;

	public string IndexerCompatibleVersions => Constants.ClientSupportIndexerVersionText;

	public string CurrentIndexerMajorVersion => IndexerClient.ApiVersion.ToString();

	protected override void OnDialogClosed()
	{
	}
}
