using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.GUI.UI
{
    public class PageGenerateWallet : TabPage
	{
		private readonly VerticalBox _verticalBoxMain = new VerticalBox { AllowPadding = true };

		private readonly Form _formPassword = new Form { AllowPadding = true };
		private readonly PasswordEntry _passwordEntry = new PasswordEntry();
		private readonly PasswordEntry _passwordEntryConfirm = new PasswordEntry();

		private readonly Button _buttonGenerateWallet = new Button("Generate");

		public PageGenerateWallet(string name = "Generate Wallet") : base(name)
        {
			InitializeComponent();
		}
		private void InitializeComponent()
		{
			Child = _verticalBoxMain;
			
			_verticalBoxMain.Children.Add(_formPassword);

			_formPassword.Children.Add("Password:", _passwordEntry);
			_formPassword.Children.Add("Confirm password:", _passwordEntryConfirm);

			_buttonGenerateWallet.Click += _buttonGenerateWalletClick;
			_verticalBoxMain.Children.Add(_buttonGenerateWallet);
		}

		private void _buttonGenerateWalletClick(object sender, EventArgs e)
		{
			var owner = Program.WindowMain;
			if (!_passwordEntry.Text.Equals(_passwordEntryConfirm.Text, StringComparison.Ordinal))
			{
				MessageBox.Show(owner, "Passwords don't match", "", MessageBoxTypes.Error);
				return;
			}

			try
			{
				var json = Shared.WalletClient.Create(_passwordEntry.Text);
				var success = json.Value<bool>("success");
				if (!success)
				{
					throw new Exception(json.Value<string>("message"));
				}

				var mnemonic = json.Value<string>("mnemonic");
				var creationTime = json.Value<string>("creationTime");

				// If no exception thrown the wallet is successfully created.
				// 4. Display mnemonic
				MessageBox.Show(owner, "Wallet is successfully created",
					"Write down the following mnemonic words:" + Environment.NewLine + Environment.NewLine +
					mnemonic + Environment.NewLine + Environment.NewLine +
					"You can recover your wallet on any computer with:" + Environment.NewLine +
					"- the mnemonic words AND " + Environment.NewLine +
					"- your password AND" + Environment.NewLine +
					$"- the wallet creation time: {creationTime.Substring(0, 10)}",
					MessageBoxTypes.Info);
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, "Couldn't the generate the wallet", ex.Message, MessageBoxTypes.Error);
				return;
			}

			try
			{
				var json = Shared.WalletClient.Load(_passwordEntry.Text);
				var success = json.Value<bool>("success");
				if (!success)
				{
					throw new Exception(json.Value<string>("message"));
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, "Couldn't decrypt the wallet", ex.Message, MessageBoxTypes.Error);
				return;
			}

			Shared.ShowAliceBob();
		}
	}
}
