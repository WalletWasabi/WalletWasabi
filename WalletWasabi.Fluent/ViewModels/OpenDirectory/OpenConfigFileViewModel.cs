using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Order = 4,
	Category = SearchCategory.Open,
	Title = "OpenConfigFileViewModel_Title",
	Keywords = "OpenConfigFileViewModel_Keywords",
	IconName = "document_regular")]
public partial class OpenConfigFileViewModel : OpenFileViewModel
{
	public OpenConfigFileViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string FilePath => UiContext.Config.ConfigFilePath;
}
