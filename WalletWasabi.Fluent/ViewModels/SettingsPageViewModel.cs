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
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui;
using Splat;
using WalletWasabi.Fluent.AddWallet.CreateWallet;
using WalletWasabi.Fluent.AddWallet.Common;
using System.IO;

namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private string _randomString;

		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new HomePageViewModel(screen)));
			CreateWalletCommand = ReactiveCommand.Create(() =>
			{
				var global = Locator.Current.GetService<Global>();

				screen.Router.Navigate.Execute(new EnterPasswordViewModel(screen, global, Path.GetRandomFileName()));
			});

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
		public Interaction<string, bool> ConfirmSetting { get; }
		public ICommand ChangeThemeCommand { get; }
		public ICommand CreateWalletCommand { get; }

		public override string IconName => "settings_regular";
	}
}