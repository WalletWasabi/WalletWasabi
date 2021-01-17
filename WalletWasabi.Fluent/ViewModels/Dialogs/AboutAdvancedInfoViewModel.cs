using System;
using System.Reactive;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class AboutAdvancedInfoViewModel : DialogViewModelBase<Unit>
	{
		public AboutAdvancedInfoViewModel()
		{
			NextCommand = CancelCommand;
		}

		public static Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public static Version HwiVersion => Constants.HwiVersion;

		public static string BackendCompatibleVersions => Constants.ClientSupportBackendVersionText;

		public static string CurrentBackendMajorVersion => WasabiClient.ApiVersion.ToString();

		protected override void OnDialogClosed()
		{
		}
	}
}