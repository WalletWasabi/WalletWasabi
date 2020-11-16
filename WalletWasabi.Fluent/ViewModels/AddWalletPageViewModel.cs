using ReactiveUI;
using System;
using System.IO;
using System.Windows.Input;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using WalletWasabi.Stores;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

		public AddWalletPageViewModel(NavigationStateViewModel navigationState, WalletManager walletManager,
			BitcoinStore store, Network network) : base(navigationState, NavigationTarget.DialogScreen)
		{
			Title = "Add Wallet";

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x && !Validations.Any);

			RecoverWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				NavigateTo(new RecoverWalletViewModel(navigationState, WalletName, network, walletManager), NavigationTarget.DialogScreen);
			});

			ConnectHardwareWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await navigationState.DialogScreen.Invoke().Router.Navigate.Execute(
					new ConnectHardwareWalletViewModel(navigationState, WalletName, network, walletManager));
			});

			CreateWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var enterPassword = new EnterPasswordViewModel(
						navigationState,
						NavigationTarget.DialogScreen,
						"Type the password of the wallet and click Continue.");

					NavigateTo(enterPassword, NavigationTarget.DialogScreen);

					var result = await enterPassword.GetDialogResultAsync();

					if (result is { } password)
					{
						var (km, mnemonic) = await Task.Run(
							() =>
							{
								var walletGenerator = new WalletGenerator(
									walletManager.WalletDirectories.WalletsDir,
									network)
								{
									TipHeight = store.SmartHeaderChain.TipHeight
								};
								return walletGenerator.GenerateWallet(WalletName, password);
							});

						NavigateTo(new RecoveryWordsViewModel(navigationState, km, mnemonic, walletManager), NavigationTarget.DialogScreen, true);
					}
					else
					{
						ClearNavigation();
					}
				});

			this.ValidateProperty(x => x.WalletName, errors => ValidateWalletName(errors, walletManager, WalletName));
		}

		private void ValidateWalletName(IValidationErrors errors, WalletManager walletManager, string walletName)
		{
			string walletFilePath = Path.Combine(walletManager.WalletDirectories.WalletsDir, $"{walletName}.json");

			if (string.IsNullOrEmpty(walletName))
			{
				return;
			}

			if (walletName.IsTrimmable())
			{
				errors.Add(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
				return;
			}

			if (File.Exists(walletFilePath))
			{
				errors.Add(ErrorSeverity.Error,
					$"A wallet named {walletName} already exists. Please try a different name.");
				return;
			}

			if (!WalletGenerator.ValidateWalletName(walletName))
			{
				errors.Add(ErrorSeverity.Error, "Selected Wallet is not valid. Please try a different name.");
			}
		}

		public override string IconName => "add_circle_regular";

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}

		public bool OptionsEnabled
		{
			get => _optionsEnabled;
			set => this.RaiseAndSetIfChanged(ref _optionsEnabled, value);
		}

		public ICommand CreateWalletCommand { get; }
		public ICommand RecoverWalletCommand { get; }
		public ICommand ConnectHardwareWalletCommand { get; }
	}
}