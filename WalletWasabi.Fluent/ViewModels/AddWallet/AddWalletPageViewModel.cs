using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Windows.Input;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using WalletWasabi.Stores;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using System.Threading.Tasks;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Legal;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;
		protected LegalDocuments _legalDocuments;

		public AddWalletPageViewModel(LegalDocuments legalDocuments, WalletManager walletManager,
			BitcoinStore store, Network network)
		{
			Title = "Add Wallet";
			_legalDocuments = legalDocuments;

			this.WhenAnyValue(x => x.WalletName)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x && !Validations.Any);

			RecoverWalletCommand = ReactiveCommand.Create(() =>
			{
				NavigateTo(new RecoverWalletViewModel(WalletName, network, walletManager));
			});

			ImportWalletCommand = ReactiveCommand.Create(() => new ImportWalletViewModel(WalletName, walletManager));

			CreateWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var result = await NavigateDialog(new EnterPasswordViewModel(
						"Type the password of the wallet and click Continue."));

					if (result is { } password)
					{
						IsBusy = true;

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

						NavigateTo(new RecoveryWordsViewModel(km, mnemonic, walletManager), true);

						IsBusy = false;
					}
				});

			this.ValidateProperty(x => x.WalletName, errors => ValidateWalletName(errors, walletManager, WalletName));
		}

		public override NavigationTarget DefaultTarget => NavigationTarget.DialogScreen;

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			this.RaisePropertyChanged(WalletName);

			if (!inStack)
			{
				WalletName = "";

				var termsAndConditions = new TermsAndConditionsViewModel(_legalDocuments, this);

				NavigateTo(termsAndConditions);
			}
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
		public ICommand ImportWalletCommand { get; }
	}
}
