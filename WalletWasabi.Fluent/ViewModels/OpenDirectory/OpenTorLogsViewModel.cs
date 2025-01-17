using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.Open,
	Title = "OpenTorLogsViewModel_Title",
	Keywords = "OpenTorLogsViewModel_Keywords",
	IconName = "document_regular")]
public partial class OpenTorLogsViewModel : OpenFileViewModel
{
	public OpenTorLogsViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string FilePath => UiContext.Config.TorLogFilePath;
}
