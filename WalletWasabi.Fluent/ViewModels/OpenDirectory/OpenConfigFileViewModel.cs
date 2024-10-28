using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

[NavigationMetaData(
	Order = 4,
	Category = SearchCategory.Open,
	IconName = "document_regular",
	IsLocalized = true)]
public partial class OpenConfigFileViewModel : OpenFileViewModel
{
	public OpenConfigFileViewModel(UiContext uiContext) : base(uiContext)
	{
	}

	public override string FilePath => UiContext.Config.ConfigFilePath;
}
