using System.IO;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Helpers;

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
	public partial class OpenWalletsFolderViewModel : TriggerCommandViewModel
	{
		public OpenWalletsFolderViewModel()
		{
			Guard.NotNull(nameof(Services.WalletManager), Services.WalletManager);

			TargetCommand = ReactiveCommand.Create(
				() => IoHelpers.OpenFolderInFileExplorer(Services.WalletManager.WalletDirectories.WalletsDir));
		}

		public override ICommand TargetCommand { get; }
	}
}