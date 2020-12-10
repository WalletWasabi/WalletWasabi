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
		private readonly Global _global;

		public OpenTorLogsViewModel(Global global)
		{
			_global = global;
		}

		public override ICommand TargetCommand =>
			ReactiveCommand.Create(() => FileHelpers.OpenFileInTextEditorAsync(_global.TorSettings.LogFilePath));
	}
}