using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.RecoverWallet;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private bool _optionsEnabled;
		private string _walletName = "";

		public AddWalletPageViewModel(IScreen screen, WalletManager walletManager, BitcoinStore store, Network network)
			: base(screen)
		{
			Title = "Add Wallet";

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x);

			RecoverWalletCommand = ReactiveCommand.Create(
				async () =>
				{
					var result = await PasswordInteraction.Handle("Type the password of the wallet you intend to recover and click Continue.").ToTask();

					if (result is { } password)
					{
						await screen.Router.Navigate.Execute(
							new RecoverWalletViewModel(screen, WalletName, network, password, walletManager));
					}
				});

			CreateWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var result = await PasswordInteraction.Handle("Type your new wallet's password below and click Continue.").ToTask();

					if (result is { } password)
					{
						var (km, mnemonic) = await Task.Run(
							() =>
							{
								var walletGenerator =
									new WalletGenerator(walletManager.WalletDirectories.WalletsDir, network)
									{
										TipHeight = store.SmartHeaderChain.TipHeight
									};
								return walletGenerator.GenerateWallet(WalletName, password);
							});

						await screen.Router.Navigate.Execute(
							new RecoveryWordsViewModel(screen, km, mnemonic, walletManager));
					}
				});

			PasswordInteraction = new Interaction<string, string?>();

			PasswordInteraction.RegisterHandler(
				async interaction => interaction.SetOutput(await new EnterPasswordViewModel(interaction.Input).ShowDialogAsync()));
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

		private Interaction<string, string?> PasswordInteraction { get; }

		public ICommand CreateWalletCommand { get; }

		public ICommand RecoverWalletCommand { get; }
	}
}