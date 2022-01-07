namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Title = "Config File",
	Caption = "",
	Order = 4,
	Category = "Open",
	Keywords = new[]
	{
			"Browse", "Open", "Config", "File"
	},
	IconName = "document_regular")]
public partial class OpenConfigFileViewModel : OpenFileViewModel
{
	public override string FilePath => Services.Config.FilePath;
}
