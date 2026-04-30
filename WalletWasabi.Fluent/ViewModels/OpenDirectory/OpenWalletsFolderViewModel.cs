using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

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
	public OpenWalletsFolderViewModel(UiContext uiContext) : base(uiContext)
	{
		TargetCommand = ReactiveCommand.Create(
			() => UiContext.FileSystem.OpenFolderInFileExplorer(UiContext.Config.WalletsDir));
	}

	public override ICommand TargetCommand { get; }
}
