using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Title = "Logs",
	Caption = "",
	Order = 2,
	Category = "Open",
	Keywords = new[]
	{
			"Browse", "Open", "Logs"
	},
	IconName = "document_regular")]
public partial class OpenLogsViewModel : OpenFileViewModel
{
	public OpenLogsViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string FilePath => UiContext.Config.LoggerFilePath;
}
