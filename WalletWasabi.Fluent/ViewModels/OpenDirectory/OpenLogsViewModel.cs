using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.Open,
	Title = "OpenLogsViewModel_Title",
	Keywords = "OpenLogsViewModel_Keywords",
	IconName = "document_regular")]
public partial class OpenLogsViewModel : OpenFileViewModel
{
	public OpenLogsViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string FilePath => UiContext.Config.LoggerFilePath;
}
