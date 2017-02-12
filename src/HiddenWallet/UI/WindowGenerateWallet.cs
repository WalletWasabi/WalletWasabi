using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;
using HiddenWallet.HiddenBitcoin.KeyManagement;

namespace HiddenWallet.UI
{
    public class WindowGenerateWallet : Window
    {
		private string WalletAlreadyExistsMessage = $"A wallet, named '{Config.WalletFileName}' already exists." + Environment.NewLine +
					$"Please change the '{nameof(Config.WalletFileName)}' setting in your configuration file: '{ConfigFileSerializer.ConfigFilePath}'";

		private Form _formPassword;
		private PasswordEntry _passwordEntry;
		private PasswordEntry _passwordEntryConfirm;

		private VerticalBox _verticalBoxMain;

		private Button _buttonGenerateWallet;
		private Button _buttonRecoverWallet;

		private Button _buttonConfig;

	    private WindowConfig _windowConfig;
	    private WindowRecoverWallet _windowRecoverWallet;

		public WindowGenerateWallet(string title = "Generate a wallet", int width = 320, int height = 240) : base(title, width, height, hasMenubar: false)
		{
			//Do not center, it won't work on linux! https://github.com/andlabs/libui/issues/183
			//StartPosition = WindowStartPosition.CenterScreen;
			AllowMargins = true;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			_verticalBoxMain = new VerticalBox { AllowPadding = true };
			Child = _verticalBoxMain;

			_formPassword = new Form { AllowPadding = true };
			_verticalBoxMain.Children.Add(_formPassword);

			_passwordEntry = new PasswordEntry();
			_formPassword.Children.Add("Password:", _passwordEntry);
			_passwordEntryConfirm = new PasswordEntry();
			_formPassword.Children.Add("Confirm password:", _passwordEntryConfirm);

			_buttonGenerateWallet = new Button("Generate new wallet");
			_buttonGenerateWallet.Click += _buttonGenerateWalletClick;
			_verticalBoxMain.Children.Add(_buttonGenerateWallet);

			_buttonRecoverWallet = new Button("Recover another wallet...");
			_verticalBoxMain.Children.Add(_buttonRecoverWallet);
			_buttonRecoverWallet.Click += _buttonRecoverWallet_Click;

			_buttonConfig = new Button("Configuration...");
			_verticalBoxMain.Children.Add(_buttonConfig);
			_buttonConfig.Click += delegate
			{
				_windowConfig = new WindowConfig();
				_windowConfig.Show();
			};
		}

		private void _buttonRecoverWallet_Click(object sender, EventArgs e)
		{
			if (File.Exists(Config.WalletFileRelativePath))
			{
				MessageBox.Show(this, "Cannot the recover wallet", WalletAlreadyExistsMessage, MessageBoxTypes.Error);
				return;
			}

			MessageBox.Show(this,$"Network: {Config.Network}" , $"Your software is configured to use the Bitcoin {Config.Network} network.", MessageBoxTypes.Info);

			_windowRecoverWallet = new WindowRecoverWallet();
			_windowRecoverWallet.Show();
		}

		private void _buttonGenerateWalletClick(object sender, EventArgs e)
		{
			if (!_passwordEntry.Text.Equals(_passwordEntryConfirm.Text, StringComparison.Ordinal))
			{
				MessageBox.Show(this, "Passwords don't match", "", MessageBoxTypes.Error);
				return;
			}

			if(File.Exists(Config.WalletFileRelativePath))
			{
				MessageBox.Show(this, "Cannot the generate wallet", WalletAlreadyExistsMessage, MessageBoxTypes.Error);
				return;
			}

			try
			{
				// 3. Create wallet
				string mnemonic;
				var safe = Safe.Create(out mnemonic, _passwordEntry.Text, Config.WalletFileRelativePath, Config.Network, DateTimeOffset.UtcNow);
				
				// If no exception thrown the wallet is successfully created.
				// 4. Display mnemonic
				MessageBox.Show(this, "Wallet is successfully created",
					"Write down the following mnemonic words:" + Environment.NewLine+ Environment.NewLine +
					mnemonic + Environment.NewLine + Environment.NewLine +
					"You can recover your wallet on any computer with:" + Environment.NewLine +
					"- the mnemonic words AND " + Environment.NewLine +
					"- your password AND" + Environment.NewLine +
					$"- the wallet creation time: {safe.GetCreationTimeString().Substring(0,10)}",
					MessageBoxTypes.Info);
			}
			catch(Exception ex)
			{
				MessageBox.Show(this, "Cannot the generate wallet", ex.Message, MessageBoxTypes.Error);
				return;
			}
		}

		protected override void Destroy()
		{
			_windowRecoverWallet?.Dispose();
			_windowConfig?.Dispose();
			base.Destroy();
		}
	}
}
