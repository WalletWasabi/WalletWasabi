using System;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class AboutAdvancedInfoViewModel : DialogViewModelBase<object>
	{
		public AboutAdvancedInfoViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget) : base(navigationState, navigationTarget)
		{
		}

		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public Version HwiVersion => Constants.HwiVersion;

		public string BackendCompatibleVersions => Constants.ClientSupportBackendVersionText;

		public string CurrentBackendMajorVersion => WasabiClient.ApiVersion.ToString();

		protected override void OnDialogClosed()
		{
		}
	}
}