using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.UI
{
    public class WindowGenerateWallet : Window
    {
		private Form _formPassword;
		private PasswordEntry _passwordEntry;
		private PasswordEntry _passwordEntryConfirm;

		private VerticalBox _verticalBoxGenerateWallet;

		private Button _buttonGenerateWallet;
		private Button _buttonRecoverWallet;

		private Button _advancedOptions;

		public WindowGenerateWallet(string title = "HiddenWallet", int width = 300, int height = 100) : base(title, width, height, hasMenubar: false)
		{
			//StartPosition = WindowStartPosition.CenterScreen;
			AllowMargins = true;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			_verticalBoxGenerateWallet = new VerticalBox { AllowPadding = true };
			Child = _verticalBoxGenerateWallet;

			_formPassword = new Form { AllowPadding = true };
			_verticalBoxGenerateWallet.Children.Add(_formPassword);

			_passwordEntry = new PasswordEntry();
			_formPassword.Children.Add("Password:", _passwordEntry);
			_passwordEntryConfirm = new PasswordEntry();
			_formPassword.Children.Add("Confirm password:", _passwordEntryConfirm);

			_buttonGenerateWallet = new Button("Generate new wallet");
			_buttonGenerateWallet.Click += _buttonGenerateWalletClick;
			_verticalBoxGenerateWallet.Children.Add(_buttonGenerateWallet);

			_buttonRecoverWallet = new Button("Recover another wallet...");
			_verticalBoxGenerateWallet.Children.Add(_buttonRecoverWallet);

			_advancedOptions = new Button("Advanced options");
			_verticalBoxGenerateWallet.Children.Add(_advancedOptions);
			_advancedOptions.Click += _advancedOptions_Click;
		}

		private void _advancedOptions_Click(object sender, EventArgs e)
		{
			new WindowConfig().Show();
		}

		private void _buttonGenerateWalletClick(object sender, EventArgs e)
		{
			if (!_passwordEntry.Text.Equals(_passwordEntryConfirm.Text, StringComparison.Ordinal))
			{
				MessageBox.Show("Passwords do not match", "", MessageBoxTypes.Info);
			}
		}
	}
}
