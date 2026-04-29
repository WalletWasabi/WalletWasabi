using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Title = "Data Folder",
	Caption = "",
	Order = 0,
	Category = "Open",
	Keywords = new[]
	{
			"Browse", "Open", "Data", "Folder"
	},
	IconName = "folder_regular")]
public partial class OpenDataFolderViewModel : TriggerCommandViewModel
{
	public OpenDataFolderViewModel(UiContext uiContext) : base(uiContext)
	{
		TargetCommand = ReactiveCommand.Create(() => UiContext.FileSystem.OpenFolderInFileExplorer(UiContext.Config.DataDir));
	}

	public override ICommand TargetCommand { get; }
}
