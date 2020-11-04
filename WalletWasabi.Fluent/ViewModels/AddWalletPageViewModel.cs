using ReactiveUI;
using System;
using System.Windows.Input;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using WalletWasabi.Stores;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.RecoverWallet;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

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
					var result = await RecoveryPagePasswordInteraction.Handle("").ToTask();

					if (result is { } password)
					{
						await screen.Router.Navigate.Execute(
							new RecoverWalletViewModel(screen, WalletName, network, password, walletManager));
					}
				});

			CreateWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var result = await CreateWalletPagePasswordInteraction.Handle("").ToTask();

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

			CreateWalletPagePasswordInteraction = new Interaction<string, string?>();
			RecoveryPagePasswordInteraction = new Interaction<string, string?>();

			CreateWalletPagePasswordInteraction.RegisterHandler(
				async interaction => interaction.SetOutput(await new EnterPasswordViewModel().ShowDialogAsync()));

			RecoveryPagePasswordInteraction.RegisterHandler(
				async interaction =>
					interaction.SetOutput(
						await new EnterPasswordViewModel(
								"Type the password of the wallet you intend to recover and click Continue.")
							.ShowDialogAsync()));
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

		private Interaction<string, string?> CreateWalletPagePasswordInteraction { get; }
		public Interaction<string, string?> RecoveryPagePasswordInteraction { get; }

		public ICommand CreateWalletCommand { get; }
		public ICommand RecoverWalletCommand { get; }
	}
}