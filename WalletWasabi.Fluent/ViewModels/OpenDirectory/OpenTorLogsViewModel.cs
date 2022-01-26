namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Title = "Tor Logs",
	Caption = "",
	Order = 3,
	Category = "Open",
	Keywords = new[]
	{
			"Browse", "Open", "Tor", "Logs"
	},
	IconName = "document_regular")]
public partial class OpenTorLogsViewModel : OpenFileViewModel
{
	public override string FilePath => Services.TorSettings.LogFilePath;
}
