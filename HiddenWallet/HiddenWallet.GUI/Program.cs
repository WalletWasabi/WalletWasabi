using System;
using DevZH.UI;

namespace HiddenWallet.GUI.UI
{
	public class Program
	{
		public static WindowMain WindowMain;

		public static void Main(string[] args)
		{
			var app = new Application(hiddenConsole: true);
			WindowMain = new WindowMain("HiddenWallet v0.3 (experimental) - nopara73");
			app.Run(WindowMain);
		}
	}
}
