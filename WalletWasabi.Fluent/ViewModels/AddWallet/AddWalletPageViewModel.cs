using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.NavBar;

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
	public partial class AddWalletPageViewModel : DialogViewModelBase<Unit>
	{
		[AutoNotify] private string _walletName = "";

		public AddWalletPageViewModel()
		{
			SelectionMode = NavBarItemSelectionMode.Button;

			var canExecute = this.WhenAnyValue(x => x.WalletName)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => !string.IsNullOrWhiteSpace(x) && !Validations.Any);

			NextCommand = ReactiveCommand.Create(OnNext, canExecute);

			OpenCommand = ReactiveCommand.Create(
				async () =>
			{
				MainViewModel.Instance.IsOobeBackgroundVisible = true;

				await NavigateDialogAsync(this, NavigationTarget.DialogScreen);

				MainViewModel.Instance.IsOobeBackgroundVisible = false;
			});

			this.ValidateProperty(x => x.WalletName, errors => ValidateWalletName(errors, WalletName));
		}

		private void OnNext()
		{
			Navigate().To(new SelectWalletCreationOptionViewModel(WalletName));
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

		protected override void OnNavigatedFrom(bool isInHistory)
		{
			base.OnNavigatedFrom(isInHistory);
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
