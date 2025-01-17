using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.OpenDirectory;

public abstract class OpenFileViewModel : TriggerCommandViewModel
{
	public OpenFileViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
	}

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
				await ShowErrorAsync(Lang.Resources.OpenFileViewModel_Error_Generic_Title, ex.ToUserFriendlyString(), Lang.Resources.OpenFileViewModel_Error_Generic_Caption);
			}
		});
}
