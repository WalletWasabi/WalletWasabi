using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() =>
			{
				screen.Router.Navigate.Execute(new HomePageViewModel(screen));
			});

			OpenDialogCommand = ReactiveCommand.Create(async () =>
			{
				var x = new TestDialogViewModel(MainViewModel.Instance);
				var result = await x.ShowDialogAsync();
			});
		}

		public ICommand NextCommand { get; }
		public ICommand OpenDialogCommand { get; }

		public override string IconName => "settings_regular";
	}
}
