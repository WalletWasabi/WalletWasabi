using System.IO;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory
{
	[NavigationMetaData(
		Title = "Wallet Folder",
		Caption = "",
		Order = 1,
		Category = "Open",
		Keywords = new[]
		{
			"Browse", "Open", "Wallet", "Folder"
		},
		IconName = "folder_regular")]
	public partial class OpenWalletFolderViewModel : TriggerCommandViewModel
	{
		private readonly WalletManagerViewModel _walletManager;

		public OpenWalletFolderViewModel(WalletManagerViewModel walletManager)
		{
			_walletManager = walletManager;
		}

		public override ICommand TargetCommand => ReactiveCommand.Create(() =>
			IoHelpers.OpenFolderInFileExplorer(_walletManager.Model.WalletDirectories.WalletsDir));
	}
}