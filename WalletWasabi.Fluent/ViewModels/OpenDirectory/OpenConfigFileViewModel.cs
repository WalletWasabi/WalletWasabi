using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

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
		public override ICommand TargetCommand =>
			ReactiveCommand.Create(async () =>
			{
				try
				{
					await FileHelpers.OpenFileInTextEditorAsync(Services.Config.FilePath);
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Open", ex.Message, "Wasabi was unable to open the file");
				}
			});
	}
}
