using System;
using System.Linq;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui;
using Splat;

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

				var walletGenerator = new WalletGenerator(global.WalletManager.WalletDirectories.WalletsDir, global.Network);
				walletGenerator.TipHeight = global.BitcoinStore.SmartHeaderChain.TipHeight;
				var (km, mnemonic) = walletGenerator.GenerateWallet("TestWallet", "12345");

				screen.Router.Navigate.Execute(new RecoveryWordsViewModel(screen, km, mnemonic));
			});

			OpenDialogCommand = ReactiveCommand.Create(async () =>
			{
				var x = new TestDialogViewModel();
				var result = await x.ShowDialogAsync(MainViewModel.Instance);
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
		public ICommand ChangeThemeCommand { get; }
		public ICommand CreateWalletCommand { get; }

		public override string IconName => "settings_regular";
	}
}
