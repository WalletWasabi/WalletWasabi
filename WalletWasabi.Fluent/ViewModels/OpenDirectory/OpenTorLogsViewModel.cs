using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

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
			ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					await FileHelpers.OpenFileInTextEditorAsync(Services.TorSettings.LogFilePath);
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Open", ex.Message, "Wasabi was unable to open the file");
				}
			});
	}
}
