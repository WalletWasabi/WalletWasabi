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

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

		public AddWalletPageViewModel(IScreen screen, WalletManager walletManager, BitcoinStore store, Network network) : base(screen)
		{
			Title = "Add Wallet";

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x);

			RecoverWalletCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new RecoveryPageViewModel(screen)));

			CreateWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var result = await PasswordInteraction.Handle("").ToTask();

					if (result is { } password)
					{
						var walletGenerator = new WalletGenerator(walletManager.WalletDirectories.WalletsDir, network)
						{
							TipHeight = store.SmartHeaderChain.TipHeight
						};
						var (km, mnemonic) = walletGenerator.GenerateWallet(WalletName, password);
						await screen.Router.Navigate.Execute(
							new RecoveryWordsViewModel(screen, km, mnemonic, walletManager));
					}
				});

			PasswordInteraction = new Interaction<string, string>();
			PasswordInteraction.RegisterHandler(
				async interaction => interaction.SetOutput(await new EnterPasswordViewModel(screen).ShowDialogAsync()));
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

		public Interaction<string, string> PasswordInteraction { get; }

		public ICommand CreateWalletCommand { get; }
		public ICommand RecoverWalletCommand { get; }
	}
}