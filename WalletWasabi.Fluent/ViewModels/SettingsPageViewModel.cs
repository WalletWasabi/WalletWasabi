using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private string _randomString;

		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new SettingsPageViewModel(screen)));

			OpenDialogCommand = ReactiveCommand.CreateFromTask(async () => await ConfirmSetting.Handle("Please confirm the setting:").ToTask());

			ConfirmSetting = new Interaction<string, bool>();

			ConfirmSetting.RegisterHandler(
				async interaction =>
				{
					var x = new TestDialogViewModel(screen, interaction.Input);
					var result = await x.ShowDialogAsync(MainViewModel.Instance);
					interaction.SetOutput(result);
				});

			ChangeThemeCommand = ReactiveCommand.Create(() =>
			{
				var currentTheme = Application.Current.Styles.Select(x => (StyleInclude)x).FirstOrDefault(x => x.Source is { } && x.Source.AbsolutePath.Contains("Themes"));

				if (currentTheme?.Source is { } src)
				{
					var themeIndex = Application.Current.Styles.IndexOf(currentTheme);

					var newTheme = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.xaml"))
					{
						Source = new Uri($"avares://WalletWasabi.Fluent/Styles/Themes/{(src.AbsolutePath.Contains("Light") ? "BaseDark" : "BaseLight")}.xaml")
					};

					Application.Current.Styles[themeIndex] = newTheme;
				}
			});
		}

		public ICommand NextCommand { get; }
		public ICommand OpenDialogCommand { get; }
		public Interaction<string, bool> ConfirmSetting { get; }
		public ICommand ChangeThemeCommand { get; }

		public override string IconName => "settings_regular";
	}
}