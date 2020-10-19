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
				//var walletGenerator = new WalletGenerator(Global.WalletManager.WalletDirectories.WalletsDir, Global.Network);
				//walletGenerator.TipHeight = Global.BitcoinStore.SmartHeaderChain.TipHeight;
				//var (km, mnemonic) = walletGenerator.GenerateWallet(WalletName, Password);
				//new GenerateWalletSuccessViewModel(Owner, km, mnemonic);

				screen.Router.Navigate.Execute(new RecoveryWordsViewModel(screen));
			});

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
