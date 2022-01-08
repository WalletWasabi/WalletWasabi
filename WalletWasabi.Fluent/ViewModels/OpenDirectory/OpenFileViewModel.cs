using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

public abstract class OpenFileViewModel : TriggerCommandViewModel
{
	public abstract string FilePath { get; }

	public override ICommand TargetCommand =>
		ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				await FileHelpers.OpenFileInTextEditorAsync(FilePath);
			}
			catch (Exception ex)
			{
				await ShowErrorAsync("Open", ex.Message, "Wasabi was unable to open the file");
			}
		});
}
