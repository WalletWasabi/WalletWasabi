using System;
using ReactiveUI;
using System.Windows.Input;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new HomePageViewModel(screen)));
			ChangeThemeCommand = ReactiveCommand.Create(() =>
			{
				var light = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.xaml"))
				{
					Source = new Uri("avares://WalletWasabi.Fluent/Styles/Themes/BaseLight.xaml")
				};
				var dark = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.xaml"))
				{
					Source = new Uri("avares://WalletWasabi.Fluent/Styles/Themes/BaseDark.xaml")
				};

				Application.Current.Styles[0] = ((StyleInclude)Application.Current.Styles[0]).Source.AbsolutePath == light.Source.AbsolutePath ? dark : light;
			});
		}

		public ICommand NextCommand { get; }
		public ICommand ChangeThemeCommand { get; }

		public override string IconName => "settings_regular";
	}
}
