using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;
using HiddenWallet.UI;

namespace HiddenWallet
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var app = new Application(hiddenConsole: true))
			{
				var askPassword = new WindowAskPassword();
				app.Run(askPassword);
			}
		}
	}
}
