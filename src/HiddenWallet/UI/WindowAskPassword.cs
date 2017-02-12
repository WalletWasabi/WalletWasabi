using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.UI
{
    public class WindowAskPassword : Window
	{
		private Form _form;
		private PasswordEntry _passwordEntry;
		private PasswordEntry _PasswordEntryConfirm;

		private VerticalBox _verticalBoxButtons;
		private HorizontalBox _horizontalBoxButtons;
		private Button _buttonOk;

		public WindowAskPassword(string title = "Generate a wallet", int width = 300, int height = 100, bool hasMenubar = false) : base(title, width, height, hasMenubar)
		{
			StartPosition = WindowStartPosition.CenterScreen;
			AllowMargins = true;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			_verticalBoxButtons = new VerticalBox { AllowPadding = true };
			Child = _verticalBoxButtons;

			_form = new Form { AllowPadding = true };
			_verticalBoxButtons.Children.Add(_form);

			_passwordEntry = new PasswordEntry();
			_form.Children.Add("Password:", _passwordEntry);
			_PasswordEntryConfirm = new PasswordEntry();
			_form.Children.Add("Confirm password:", _PasswordEntryConfirm);

			_horizontalBoxButtons = new HorizontalBox {AllowPadding = true};
			_verticalBoxButtons.Children.Add(_horizontalBoxButtons);

			_buttonOk = new Button("Generate wallet");
			_horizontalBoxButtons.Children.Add(_buttonOk);
			_buttonOk = new Button("Recover another wallet...");
			_horizontalBoxButtons.Children.Add(_buttonOk);
		}
	}
}
