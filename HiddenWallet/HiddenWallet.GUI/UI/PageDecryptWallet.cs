using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.GUI.UI
{
    public class PageDecryptWallet : TabPage
	{
		private readonly VerticalBox _verticalBoxMain = new VerticalBox { AllowPadding = true };
		private readonly Form _formPassword = new Form { AllowPadding = true };
		private readonly PasswordEntry _passwordEntry = new PasswordEntry();

		private readonly Button _buttonDecryptWallet = new Button("Decrypt");

		public PageDecryptWallet(string name = "Decrypt Wallet") : base(name)
        {
			InitializeComponent();
		}
		private void InitializeComponent()
		{
			Child = _verticalBoxMain;

			_verticalBoxMain.Children.Add(_formPassword);
			
			_formPassword.Children.Add("Password:", _passwordEntry);
			
			_buttonDecryptWallet.Click += _buttonDecryptWallet_Click;
			_verticalBoxMain.Children.Add(_buttonDecryptWallet);
			
		}
		private void _buttonDecryptWallet_Click(object sender, EventArgs e)
		{
			var owner = Program.WindowMain;
			try
			{
				var json = Shared.WalletClient.Load(_passwordEntry.Text);
				var success = json.Value<bool>("success");
				if (!success)
				{
					throw new Exception(json.Value<string>("message"));
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show(owner, "Couldn't decrypt the wallet", ex.Message, MessageBoxTypes.Error);
				return;
			}
			Shared.ShowAliceBob();
		}
	}
}
