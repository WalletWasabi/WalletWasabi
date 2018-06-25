using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.IO;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _passwordConfirmation;
		private string _walletName;
		private bool _termsAccepted;
		private string _validationMessage;

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
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

		public string ValidationMessage
		{
			get { return _validationMessage; }
			set { this.RaiseAndSetIfChanged(ref _validationMessage, value); }
		}

		public ReactiveCommand GenerateCommand { get; }

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument<TermsAndConditionsViewModel>(new TermsAndConditionsViewModel());
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument<PrivacyPolicyViewModel>(new PrivacyPolicyViewModel());
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument<LegalIssuesViewModel>(new LegalIssuesViewModel());
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = null;
			PasswordConfirmation = null;
			WalletName = Utils.GetNextWalletName();
			TermsAccepted = false;
			ValidationMessage = null;
		}
	}
}
