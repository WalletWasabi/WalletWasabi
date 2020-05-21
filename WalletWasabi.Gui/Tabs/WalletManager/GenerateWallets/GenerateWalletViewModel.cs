using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _walletName;

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
		{
			Global = Locator.Current.GetService<Global>();
			Owner = owner;

			this.ValidateProperty(x => x.Password, ValidatePassword);

			NextCommand = ReactiveCommand.Create(DoNextCommand, this.WhenAnyValue(x => x.Validations.Any).Select(x => !x));

			NextCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public WalletManagerViewModel Owner { get; }

		private Global Global { get; }

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}

		public ReactiveCommand<Unit, Unit> NextCommand { get; }

		private void DoNextCommand()
		{
			try
			{
				var walletGenerator = new WalletGenerator(Global.WalletManager.WalletDirectories.WalletsDir, Global.Network);
				walletGenerator.TipHeight = Global.BitcoinStore.SmartHeaderChain.TipHeight;
				var (km, mnemonic) = walletGenerator.GenerateWallet(WalletName, Password);
				Owner.CurrentView = new GenerateWalletSuccessViewModel(Owner, km, mnemonic);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				NotificationHelpers.Error(ex.ToUserFriendlyString());
			}
		}

		private void ValidatePassword(IValidationErrors errors)
		{
			string password = Password;

			if (PasswordHelper.IsTrimable(password, out _))
			{
				errors.Add(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
			}

			if (PasswordHelper.IsTooLong(password, out _))
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage);
			}
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = "";
			WalletName = Global.WalletManager.WalletDirectories.GetNextWalletName();
		}
	}
}
