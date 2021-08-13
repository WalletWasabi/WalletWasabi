using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory
{
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
	public partial class OpenLogsViewModel : TriggerCommandViewModel
	{
		public override ICommand TargetCommand =>
			ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath);
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Open", ex.Message, "Wasabi was unable to open the file");
				}
			});
	}
}
