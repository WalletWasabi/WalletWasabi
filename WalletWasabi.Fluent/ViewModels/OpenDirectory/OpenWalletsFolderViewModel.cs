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
	public partial class OpenWalletsFolderViewModel : TriggerCommandViewModel
	{
		public OpenWalletsFolderViewModel(string walletDir)
		{
			TargetCommand = ReactiveCommand.Create(
				() => IoHelpers.OpenFolderInFileExplorer(walletDir));
		}

		public override ICommand TargetCommand { get; }
	}
}