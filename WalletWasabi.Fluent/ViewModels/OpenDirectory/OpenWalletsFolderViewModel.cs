using System.Windows.Input;
using WalletWasabi.Helpers;

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
		TargetCommand = ReactiveCommand.Create(() => IoHelpers.OpenFolderInFileExplorer(UiContext.Config.WalletsDir));
	}

	public override ICommand TargetCommand { get; }
}
