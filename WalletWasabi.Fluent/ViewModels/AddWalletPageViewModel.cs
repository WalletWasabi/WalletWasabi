using ReactiveUI;
using Splat;
using System;
using System.Windows.Input;
using System.Reactive.Linq;
using WalletWasabi.Fluent.AddWallet.Common;
using WalletWasabi.Gui;
using System.Reactive.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.AddWallet.CreateWallet;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

		public AddWalletPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Add Wallet";

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x);

			RecoverWalletCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new RecoveryPageViewModel(screen)));


			var global = Locator.Current.GetService<Global>();

			CreateWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var result = await PasswordInteraction.Handle("").ToTask();

				if (result is { } password)
				{
					var walletGenerator = new WalletGenerator(global.WalletManager.WalletDirectories.WalletsDir, global.Network);
					walletGenerator.TipHeight = global.BitcoinStore.SmartHeaderChain.TipHeight;
					var (km, mnemonic) = walletGenerator.GenerateWallet(WalletName, password);
					await screen.Router.Navigate.Execute(new RecoveryWordsViewModel(screen, km, mnemonic, global));
				}
			});
			PasswordInteraction = new Interaction<string, string>();
			PasswordInteraction.RegisterHandler(
				async interaction =>
				{
					var result = await new EnterPasswordViewModel(screen, global, WalletName).ShowDialogAsync(MainViewModel.Instance);
					interaction.SetOutput(result);
				});
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