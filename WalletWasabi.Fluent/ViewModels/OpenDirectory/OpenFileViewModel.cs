using System.Windows.Input;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

public abstract class OpenFileViewModel(UiContext uiContext) : TriggerCommandViewModel(uiContext)
{
	public abstract string FilePath { get; }

	public override ICommand TargetCommand =>
		ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				await UiContext.FileSystem.OpenFileInTextEditorAsync(FilePath);
			}
			catch (Exception ex)
			{
				await ShowErrorAsync("Open", ex.ToUserFriendlyString(), "Wasabi was unable to open the file");
			}
		});
}
