using System;
using System.Globalization;
using System.IO;
using DevZH.UI;
using NBitcoin;

namespace HiddenWallet.GUI.UI
{
    public class PageRecoverWallet : TabPage
	{
		private readonly VerticalBox _verticalBoxMain = new VerticalBox { AllowPadding = true };
		private readonly Form _form = new Form { AllowPadding = true };
		private readonly Entry _entryCreation = new Entry();
		private readonly Entry _entryMnemonic = new Entry();
		private readonly PasswordEntry _passwordEntry = new PasswordEntry();
		private readonly Button _buttonRecoverWallet = new Button("Recover");

		private MultilineEntry _multyLineEntryInfo;
		public PageRecoverWallet(string name = "Recover Wallet") : base(name)
        {
			InitializeComponent();
		}
		private void InitializeComponent()
		{
			Child = _verticalBoxMain;

			_verticalBoxMain.Children.Add(_form);

			_entryCreation.Text = DateTime.UtcNow.ToString("yyyy-MM-dd");

			for (int i = 1; i <= 12; i++)
			{
				_entryMnemonic.Text += ($"word{i} ");
				if (i == 12) _entryMnemonic.Text = _entryMnemonic.Text.TrimEnd();
			}

			_form.Children.Add("Syncronize transactions from:", _entryCreation);
			_form.Children.Add("Password:", _passwordEntry);
			_form.Children.Add("Mnemonic words:", _entryMnemonic, stretchy: true);

			_multyLineEntryInfo = new MultilineEntry(true)
			{
				Enabled = false,
				Text =
					"Note the wallet cannot check if your password is correct or not. If you provide a wrong password a wallet will be recovered with the provided mnemonic and password combination."
			};
			_verticalBoxMain.Children.Add(_multyLineEntryInfo, true);
			
			_buttonRecoverWallet.Click += _buttonRecoverWallet_Click;
			_verticalBoxMain.Children.Add(_buttonRecoverWallet);
		}

		private void _buttonRecoverWallet_Click(object sender, EventArgs e)
		{
			var owner = Program.WindowMain;
			try
			{
				var mnemonic = new Mnemonic(_entryMnemonic.Text.Trim());

				DateTimeOffset creation = DateTimeOffset.ParseExact(_entryCreation.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture) - TimeSpan.FromDays(1);

				var json = Shared.WalletClient.Recover(_passwordEntry.Text, mnemonic, creation.ToString("yyyy-MM-dd"));
				var success = json.Value<bool>("success");
				if (!success)
				{
					throw new Exception(json.Value<string>("message"));
				}

				MessageBox.Show(owner, "Wallet successfully recovered", "", MessageBoxTypes.Info);
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, "Wallet recovery failed", ex.Message, MessageBoxTypes.Error);
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
