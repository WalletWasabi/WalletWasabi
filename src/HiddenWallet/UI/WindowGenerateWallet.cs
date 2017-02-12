using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		private Button _buttonConfig;

		public WindowGenerateWallet(string title = "HiddenWallet", int width = 320, int height = 240) : base(title, width, height, hasMenubar: false)
		{
			//Do not center, it won't work on linux! https://github.com/andlabs/libui/issues/183
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

			_buttonConfig = new Button("Configuration...");
			_verticalBoxGenerateWallet.Children.Add(_buttonConfig);
			_buttonConfig.Click += delegate
			{
				new WindowConfig().Show(); ;
			};
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
