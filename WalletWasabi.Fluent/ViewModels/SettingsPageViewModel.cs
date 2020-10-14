using System;
using System.Linq;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;
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

			OpenDialogCommand = ReactiveCommand.Create(async () =>
			{
				var x = new TestDialogViewModel();
				var result = await x.ShowDialogAsync(MainViewModel.Instance);
			});

			ChangeThemeCommand = ReactiveCommand.Create(() =>
			{
				var currentTheme = Application.Current.Styles.Select(x => (StyleInclude)x).FirstOrDefault(x => x.Source is { } && x.Source.AbsolutePath.Contains("Themes"));

				if (currentTheme?.Source is { })
				{
					var themeIndex = Application.Current.Styles.IndexOf(currentTheme);

					var newTheme = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.xaml"))
					{
						Source = new Uri($"avares://WalletWasabi.Fluent/Styles/Themes/{(currentTheme.Source.AbsolutePath.Contains("Light") ? "BaseDark" : "BaseLight")}.xaml")
					};

					Application.Current.Styles[themeIndex] = newTheme;
				}
			});
		}

		public ICommand NextCommand { get; }
		public ICommand OpenDialogCommand { get; }
		public ICommand ChangeThemeCommand { get; }

		public override string IconName => "settings_regular";
	}
}
