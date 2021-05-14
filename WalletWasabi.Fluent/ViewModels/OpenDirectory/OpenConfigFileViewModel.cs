using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory
{
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
	public partial class OpenConfigFileViewModel : TriggerCommandViewModel
	{
		public OpenConfigFileViewModel()
		{
			Guard.NotNull(nameof(Services.Config), Services.Config);
		}

		public override ICommand TargetCommand =>
			ReactiveCommand.Create(() => FileHelpers.OpenFileInTextEditorAsync(Services.Config.FilePath));
	}
}