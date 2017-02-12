using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.UI
{
	public class WindowConfig : Window
	{
		public WindowConfig(string title = "Edit your config file", int width = 500, int height = 200) : base(title, width, height, hasMenubar: false)
		{
			StartPosition = WindowStartPosition.CenterScreen;
		}
	}
}
