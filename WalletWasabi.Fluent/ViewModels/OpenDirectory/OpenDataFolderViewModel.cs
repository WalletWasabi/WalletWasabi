using System.IO;
using System.Windows.Input;
using ReactiveUI;

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
	public OpenDataFolderViewModel()
	{
		TargetCommand = ReactiveCommand.Create(() => IoHelpers.OpenFolderInFileExplorer(Services.DataDir));
	}

	public override ICommand TargetCommand { get; }
}
