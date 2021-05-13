using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Windows.Input;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(
		Title = "Add Wallet",
		Caption = "Create, recover or import wallet",
		Order = 2,
		Category = "General",
		Keywords = new[] { "Wallet", "Add", "Create", "Recover", "Import", "Connect", "Hardware", "ColdCard", "Trezor", "Ledger" },
		IconName = "add_circle_regular",
		NavigationTarget = NavigationTarget.DialogScreen,
		NavBarPosition = NavBarPosition.Bottom)]
	public partial class AddWalletPageViewModel : NavBarItemViewModel
	{
		[AutoNotify] private string _walletName = "";
		[AutoNotify] private bool _optionsEnabled;

		public AddWalletPageViewModel(WalletManagerViewModel walletManagerViewModel)
		{
			SelectionMode = NavBarItemSelectionMode.Button;
			var network = Services.WalletManager.Network;

			var enableBack = default(IDisposable);
			this.WhenAnyValue(x => x.CurrentTarget)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					enableBack?.Dispose();
					enableBack = Navigate()
						.WhenAnyValue(y => y.CanNavigateBack)
						.Subscribe(y => EnableBack = y);
				});

			this.WhenAnyValue(x => x.WalletName)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x && !Validations.Any);

			RecoverWalletCommand = ReactiveCommand.Create(() => OnRecoverWallet(walletManagerViewModel));

			ImportWalletCommand = ReactiveCommand.CreateFromTask(async () => await OnImportWalletAsync());

			ConnectHardwareWalletCommand = ReactiveCommand.Create(() => OnConnectHardwareWallet(walletManagerViewModel));

			CreateWalletCommand = ReactiveCommand.CreateFromTask(async () => await OnCreateWalletAsync(network));

			this.ValidateProperty(x => x.WalletName, errors => ValidateWalletName(errors, WalletName));

			EnableAutoBusyOn(CreateWalletCommand);
		}

		private static void ValidateWalletName(IValidationErrors errors, string walletName)
		{
			string walletFilePath = Path.Combine(Services.WalletManager.WalletDirectories.WalletsDir, $"{walletName}.json");

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
				errors.Add(
					ErrorSeverity.Error,
					$"A wallet named {walletName} already exists. Please try a different name.");
				return;
			}

			if (!WalletGenerator.ValidateWalletName(walletName))
			{
				errors.Add(ErrorSeverity.Error, "Selected Wallet is not valid. Please try a different name.");
			}
		}

		public ICommand CreateWalletCommand { get; }

		public ICommand RecoverWalletCommand { get; }

		public ICommand ImportWalletCommand { get; }

		public ICommand ConnectHardwareWalletCommand { get; }

		private void OnRecoverWallet(WalletManagerViewModel walletManagerViewModel)
		{
			Navigate().To(new RecoverWalletViewModel(WalletName));
		}

		private void OnConnectHardwareWallet(WalletManagerViewModel walletManagerViewModel)
		{
			Navigate().To(new ConnectHardwareWalletViewModel(WalletName, walletManagerViewModel));
		}

		private async Task OnImportWalletAsync()
		{
			try
			{
				var filePath = await FileDialogHelper.ShowOpenFileDialogAsync("Import wallet file", new[] { "json" });

				if (filePath is null)
				{
					return;
				}

				var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, WalletName, filePath);

				Navigate().To(new AddedWalletPageViewModel(keyManager));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(),
					"The wallet file was not valid or compatible with Wasabi.");
			}
		}

		private async Task OnCreateWalletAsync(Network network)
		{
			var dialogResult = await NavigateDialogAsync(
				new CreatePasswordDialogViewModel("Type the password of the wallet and click Continue.", enableEmpty: true, enableCancel: Services.WalletManager.HasWallet()));

			if (dialogResult.Result is { } password)
			{
				var (km, mnemonic) = await Task.Run(
					() =>
					{
						var walletGenerator = new WalletGenerator(
							Services.WalletManager.WalletDirectories.WalletsDir,
							network)
						{
							TipHeight = Services.BitcoinStore.SmartHeaderChain.TipHeight
						};
						return walletGenerator.GenerateWallet(WalletName, password);
					});

				Navigate().To(new RecoveryWordsViewModel(km, mnemonic));
			}
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			var enableCancel = Services.WalletManager.HasWallet();
			SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);

			this.RaisePropertyChanged(WalletName);

			if (!isInHistory)
			{
				WalletName = "";
			}
		}
	}
}
