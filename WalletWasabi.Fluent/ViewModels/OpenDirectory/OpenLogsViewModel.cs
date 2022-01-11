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
	public override string FilePath => Logger.FilePath;
}
