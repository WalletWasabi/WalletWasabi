using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.Open,
	IconName = "document_regular",
	IsLocalized = true)]
public partial class OpenTorLogsViewModel : OpenFileViewModel
{
	public OpenTorLogsViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string FilePath => UiContext.Config.TorLogFilePath;
}
