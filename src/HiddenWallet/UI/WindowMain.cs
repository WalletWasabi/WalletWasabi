using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.UI
{
	public class WindowMain : Window
	{
		private Tab _tab;

		public WindowMain(string title = "HiddenWallet", int width = 640, int height = 480, bool hasMenubar = true) : base(title, width, height, hasMenubar)
		{
			AllowMargins = true;
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
