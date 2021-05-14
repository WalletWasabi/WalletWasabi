using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory
{
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
	public partial class OpenTorLogsViewModel : TriggerCommandViewModel
	{
		public OpenTorLogsViewModel()
		{
			Guard.NotNull(nameof(Services.TorSettings), Services.TorSettings);
		}

		public override ICommand TargetCommand =>
			ReactiveCommand.Create(() => FileHelpers.OpenFileInTextEditorAsync(Services.TorSettings.LogFilePath));
	}
}