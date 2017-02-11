using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.UI
{
    public class WindowAskPassword : Window
	{
		private Tab _tab;

		public WindowAskPassword(string title = "Please choose a password:", int width = 300, int height = 100, bool hasMenubar = false) : base(title, width, height, hasMenubar)
		{
			StartPosition = WindowStartPosition.CenterScreen;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			_tab = new Tab();
			Child = _tab;
		}
	}
}
