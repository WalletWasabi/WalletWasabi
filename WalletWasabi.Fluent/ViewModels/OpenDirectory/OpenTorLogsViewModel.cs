using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;

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
		public override ICommand TargetCommand =>
			ReactiveCommand.Create(() => FileHelpers.OpenFileInTextEditorAsync(Gui.Services.TorSettings.LogFilePath));
	}
}