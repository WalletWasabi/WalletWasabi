using ReactiveUI;
using System;
using System.IO;
using System.Windows.Input;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using WalletWasabi.Stores;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using System.Threading.Tasks;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

		public AddWalletPageViewModel(NavigationStateViewModel navigationState, WalletManager walletManager, BitcoinStore store, Network network) : base(navigationState, NavigationTarget.Dialog)
		{
			Title = "Add Wallet";

			BackCommand = ReactiveCommand.Create(() => ClearNavigation());

			CancelCommand = ReactiveCommand.Create(() => ClearNavigation());

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x && !Validations.Any);

			RecoverWalletCommand = ReactiveCommand.Create(() => navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(new RecoveryPageViewModel(navigationState)));

			CreateWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var result = await PasswordInteraction.Handle("").ToTask();

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

						await navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(
							new RecoveryWordsViewModel(navigationState, km, mnemonic, walletManager));
					}
				});

			PasswordInteraction = new Interaction<string, string?>();
			PasswordInteraction.RegisterHandler(
				async interaction => interaction.SetOutput(await new EnterPasswordViewModel().ShowDialogAsync()));

			this.ValidateProperty(x => x.WalletName, errors => ValidateWalletName(errors, walletManager, WalletName));
		}

		private void ValidateWalletName(IValidationErrors errors, WalletManager walletManager, string walletName)
		{
			string walletFilePath = Path.Combine(walletManager.WalletDirectories.WalletsDir, $"{walletName}.json");

			if (string.IsNullOrEmpty(walletName))
			{
				return;
			}

			if (PasswordHelper.IsTrimable(walletName, out _))
			{
				errors.Add(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
				return;
			}

			if (File.Exists(walletFilePath))
			{
				errors.Add(ErrorSeverity.Error, $"A wallet named {walletName} already exists. Please try a different name.");
				return;
			}

			if (!WalletGenerator.ValidateWalletName(walletName))
			{
				errors.Add(ErrorSeverity.Error, "Selected Wallet is not valid. Please try a different name.");
			}
		}

		public override string IconName => "add_circle_regular";

		public ICommand BackCommand { get; }
		public ICommand CancelCommand { get; }

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

		private Interaction<string, string?> PasswordInteraction { get; }

		public ICommand CreateWalletCommand { get; }
		public ICommand RecoverWalletCommand { get; }
	}
}