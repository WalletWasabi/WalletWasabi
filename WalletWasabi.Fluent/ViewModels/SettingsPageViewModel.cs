using ReactiveUI;
using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		public override string IconName => "settings_regular";

		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() =>
			{
				screen.Router.Navigate.Execute(new HomePageViewModel(screen));
			});
		}

		public ICommand NextCommand { get; }
	}
}
