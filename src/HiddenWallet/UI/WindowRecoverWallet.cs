using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;
using HiddenWallet.HiddenBitcoin.KeyManagement;
using NBitcoin;

namespace HiddenWallet.UI
{
    public class WindowRecoverWallet : Window
	{
		private VerticalBox _verticalBoxMain;
		private Form _form;
		private Entry _entryCreation;
		private MultilineEntry _multyLineEntryMnemonic;
		private PasswordEntry _passwordEntry;
		private Button _buttonRecoverWallet;

		private MultilineEntry _multyLineEntryInfo;

		public WindowRecoverWallet(string title = "Recover a wallet", int width = 320, int height = 240)
			: base(title, width, height, hasMenubar: false)
		{
			AllowMargins = true;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			_verticalBoxMain = new VerticalBox { AllowPadding = true };
			Child = _verticalBoxMain;

			_form = new Form {AllowPadding = true};
			_verticalBoxMain.Children.Add(_form);

			_entryCreation = new Entry();
			_entryCreation.Text = DateTime.UtcNow.ToString("yyyy-MM-dd");
			_multyLineEntryMnemonic = new MultilineEntry(isWrapping: true);
			for(int i = 1; i <= 12; i++)
			{
				_multyLineEntryMnemonic.Append($"word{i}");
				if(i != 12) _multyLineEntryMnemonic.Append(Environment.NewLine);
			}
			_passwordEntry = new PasswordEntry();

			_form.Children.Add("Syncronize transactions from:", _entryCreation);
			_form.Children.Add("Mnemonic words:", _multyLineEntryMnemonic, true);
			_form.Children.Add("Password:", _passwordEntry);

			_multyLineEntryInfo = new MultilineEntry(true)
			{
				Enabled = false,
				Text =
					"Note the wallet cannot check if your password is correct or not. If you provide a wrong password a wallet will be recovered with the provided mnemonic and password combination."
			};
			_verticalBoxMain.Children.Add(_multyLineEntryInfo, true);

			_buttonRecoverWallet = new Button("Recover wallet");
			_buttonRecoverWallet.Click += _buttonRecoverWallet_Click;
			_verticalBoxMain.Children.Add(_buttonRecoverWallet);
		}

		private void _buttonRecoverWallet_Click(object sender, EventArgs e)
		{
			try
			{
				var mnemonic = "";
				foreach (
					var word in
					_multyLineEntryMnemonic.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
				{
					mnemonic += word.Trim() + " ";
				}
				mnemonic = mnemonic.TrimEnd();

				if (!new Mnemonic(mnemonic).IsValidChecksum)
				{
					MessageBox.Show(this, "Wallet recovery failed", "Wrong mnemonic format", MessageBoxTypes.Error);
					return;
				}

				DateTimeOffset creation = DateTimeOffset.ParseExact(_entryCreation.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture) - TimeSpan.FromDays(1);
				Safe.Recover(mnemonic, _passwordEntry.Text, Config.WalletFileRelativePath, Config.Network, creation);

				MessageBox.Show(this, "Wallet recovered", "", MessageBoxTypes.Info);
				return;
			}
			catch(FormatException)
			{
				MessageBox.Show(this, "Wallet recovery failed", "Wrong mnemonic format", MessageBoxTypes.Error);
				return;
			}
			catch(NotSupportedException)
			{
				MessageBox.Show(this, "Wallet recovery failed", "Wrong mnemonic format", MessageBoxTypes.Error);
				return;
			}
			catch(Exception ex)
			{
				MessageBox.Show(this, "Wallet recovery failed", ex.Message, MessageBoxTypes.Error);
				return;
			}
		}
	}
}
