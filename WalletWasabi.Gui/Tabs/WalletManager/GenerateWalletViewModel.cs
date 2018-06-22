using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _passwordConfirmation;
		private string _walletName;
		private bool _termsAccepted;

		public GenerateWalletViewModel() : base("Generate Wallet")
		{
			GenerateCommand = ReactiveCommand.Create(() =>
			{
			},
			this.WhenAnyValue(x => x.TermsAccepted));
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public string PasswordConfirmation
		{
			get { return _passwordConfirmation; }
			set { this.RaiseAndSetIfChanged(ref _passwordConfirmation, value); }
		}

		public string WalletName
		{
			get { return _walletName; }
			set { this.RaiseAndSetIfChanged(ref _walletName, value); }
		}

		public bool TermsAccepted
		{
			get { return _termsAccepted; }
			set { this.RaiseAndSetIfChanged(ref _termsAccepted, value); }
		}

		public ReactiveCommand GenerateCommand { get; }

		public void OnTermsClicked()
		{
		}

		public void OnPrivacyClicked()
		{
		}
	}
}
