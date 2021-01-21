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
		private readonly Global _global;

		public OpenConfigFileViewModel(Global global)
		{
			_global = global;
		}

		public override ICommand TargetCommand =>
			ReactiveCommand.Create(() => FileHelpers.OpenFileInTextEditorAsync(_global.Config.FilePath));
	}
}
