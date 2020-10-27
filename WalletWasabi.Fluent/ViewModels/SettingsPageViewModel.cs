using System;
using System.Linq;
using ReactiveUI;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private string _randomString;

		public SettingsPageViewModel(NavigationStateViewModel navigationState) : base(navigationState)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() => navigationState.MainScreen().Router.Navigate.Execute(new AddWalletPageViewModel(navigationState)));

			OpenDialogCommand = ReactiveCommand.CreateFromTask(async () => await ConfirmSetting.Handle("Please confirm the setting:").ToTask());

			OpenDialogScreenCommand = ReactiveCommand.Create(() => navigationState.DialogScreen().Router.Navigate.Execute(new AddWalletPageViewModel(navigationState)));

			ConfirmSetting = new Interaction<string, bool>();

			ConfirmSetting.RegisterHandler(
				async interaction =>
				{
					var x = new TestDialogViewModel(navigationState, interaction.Input);
					var result = await x.ShowDialogAsync(navigationState.DialogHost());
					interaction.SetOutput(result);
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

			// For TextBox error look
			this.ValidateProperty(x => x.RandomString, (errors) => errors.Add(ErrorSeverity.Error, "Random Error Message"));
			this.RaisePropertyChanged(nameof(RandomString));
		}

		public string RandomString
		{
			get => _randomString;
			set => this.RaiseAndSetIfChanged(ref _randomString, value);
		}

		public ICommand NextCommand { get; }
		public ICommand OpenDialogCommand { get; }
		public ICommand OpenDialogScreenCommand { get; }
		public Interaction<string, bool> ConfirmSetting { get; }
		public ICommand ChangeThemeCommand { get; }

		public override string IconName => "settings_regular";
	}
}