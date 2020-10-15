using ReactiveUI;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new HomePageViewModel(screen)));

			OpenDialogCommand = ReactiveCommand.CreateFromTask(async () => await ConfirmSetting.Handle("Please confirm the setting:").ToTask());

			ConfirmSetting = new Interaction<string, bool>();

			// NOTE: In ReactiveUI docs this is registered in view ctor.
			ConfirmSetting.RegisterHandler(
				async interaction =>
				{
					var x = new TestDialogViewModel(interaction.Input);
					var result = await x.ShowDialogAsync(MainViewModel.Instance);
					interaction.SetOutput(result);
				});
		}

		public ICommand NextCommand { get; }
		public ICommand OpenDialogCommand { get; }
		public Interaction<string, bool> ConfirmSetting { get; }

		public override string IconName => "settings_regular";
	}
}