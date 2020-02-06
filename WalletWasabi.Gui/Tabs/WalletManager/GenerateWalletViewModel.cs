using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager
{

	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _walletName;
		private bool _termsAccepted;
		public WalletManagerViewModel Owner { get; }

		private Global Global { get; }

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
		{
			Global = Locator.Current.GetService<Global>();
			Owner = owner;

			IObservable<bool> canGenerate = Observable.CombineLatest(
				this.WhenAnyValue(x => x.TermsAccepted),
				this.WhenAnyValue(x => x.Password).Select(pw => !ValidatePassword().HasErrors),
				(terms, pw) => terms && pw);

			NextCommand = ReactiveCommand.Create(DoNextCommand, canGenerate);

			NextCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void DoNextCommand()
		{
			try
			{
				var walletGenerator = new WalletGenerator(Global.WalletsDir, Global.Network);
				walletGenerator.TermsAccepted = TermsAccepted;
				walletGenerator.TipHeight = Global.BitcoinStore.SmartHeaderChain.TipHeight;
				var (km, mnemonic) = walletGenerator.GenerateWallet(WalletName, Password);
				Owner.CurrentView = new GenerateWalletSuccessViewModel(Owner, km, mnemonic);
			}
			catch(Exception ex)
			{
				Logger.LogError(ex);
				NotificationHelpers.Error(ex.ToUserFriendlyString());
			}
		}

		public ErrorDescriptors ValidatePassword()
		{
			string password = Password;

			var errors = new ErrorDescriptors();

			if (PasswordHelper.IsTrimable(password, out _))
			{
				errors.Add(new ErrorDescriptor(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!"));
			}

			if (PasswordHelper.IsTooLong(password, out _))
			{
				errors.Add(new ErrorDescriptor(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage));
			}

			return errors;
		}

		[ValidateMethod(nameof(ValidatePassword))]
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

		public bool TermsAccepted
		{
			get => _termsAccepted;
			set => this.RaiseAndSetIfChanged(ref _termsAccepted, value);
		}

		public ReactiveCommand<Unit, Unit> NextCommand { get; }

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new LegalDocumentsViewModel());
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = "";
			WalletName = Global.GetNextWalletName();
			TermsAccepted = false;
		}
	}
}
