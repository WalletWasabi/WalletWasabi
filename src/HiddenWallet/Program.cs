using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;
using DevZH.UI.Interop;
using HiddenWallet.UI;

namespace HiddenWallet
{
	public class Program
	{
		public static void Main(string[] args)
		{
			// Load config file
			// It also creates it with default settings if doesn't exist
			Config.Load();

			var app = new Application(hiddenConsole: true);
			var askPassword = new WindowGenerateWallet(title: Config.MainInfo);
			{
				app.Run(askPassword);
			}
		}
	}
}
