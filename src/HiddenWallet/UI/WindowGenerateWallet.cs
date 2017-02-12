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

		private HorizontalBox _horizontalBoxButtons;
		private Button _buttonGenerateWallet;
		private Button _buttonRecoverWallet;

		public WindowGenerateWallet(string title = "Generate a wallet", int width = 300, int height = 100, bool hasMenubar = false) : base(title, width, height, hasMenubar)
		{
			StartPosition = WindowStartPosition.CenterScreen;
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

			_horizontalBoxButtons = new HorizontalBox {AllowPadding = true};
			_verticalBoxGenerateWallet.Children.Add(_horizontalBoxButtons);

			_buttonGenerateWallet = new Button("Generate the wallet");
			_buttonGenerateWallet.Click += _buttonGenerateWalletClick;
			_horizontalBoxButtons.Children.Add(_buttonGenerateWallet);

			_buttonRecoverWallet = new Button("Recover another wallet...");
			_horizontalBoxButtons.Children.Add(_buttonRecoverWallet);
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
