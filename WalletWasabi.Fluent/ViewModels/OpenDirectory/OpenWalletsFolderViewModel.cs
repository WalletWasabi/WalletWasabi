using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.Open,
	Title = "OpenWalletsFolderViewModel_Title",
	Keywords = "OpenWalletsFolderViewModel_Keywords",
	IconName = "folder_regular")]
public partial class OpenWalletsFolderViewModel : TriggerCommandViewModel
{
	private OpenWalletsFolderViewModel()
	{
		TargetCommand = ReactiveCommand.Create(
			() => UiContext.FileSystem.OpenFolderInFileExplorer(UiContext.Config.WalletsDir));
	}

	public override ICommand TargetCommand { get; }
}
