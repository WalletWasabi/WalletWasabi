using System;
using System.Reactive;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "About")]
	public partial class AboutAdvancedInfoViewModel : DialogViewModelBase<Unit>
	{
		public AboutAdvancedInfoViewModel()
		{
			NextCommand = CancelCommand;
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