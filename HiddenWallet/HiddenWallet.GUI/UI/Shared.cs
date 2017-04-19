using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;
using System.Net.Http;

namespace HiddenWallet.GUI.UI
{
    public static class Shared
    {
		public static WalletClient WalletClient = new WalletClient();

		public static List<TabPage> GetActivePages(TabPage except = null)
		{
			var pagesExceptThis = new List<TabPage>();

			foreach (var control in Program.WindowMain.Tab.Children)
			{
				if (control is TabPage)
				{
					if(except != null)
					{
						if(!control.Equals(except))
						{
							pagesExceptThis.Add(control as TabPage);
						}
					}
					else
					{
						pagesExceptThis.Add(control as TabPage);
					}
				}
			}

			return pagesExceptThis;
		}

	    public static void ShowAliceBob()
	    {
		    foreach (var tab in GetActivePages())
		    {
			    Program.WindowMain.Tab.Children.Remove(tab);
		    }

			Program.WindowMain.Tab.Children.Add(Program.WindowMain.PageAliceWallet);
			Program.WindowMain.Tab.Children.Add(Program.WindowMain.PageBobWallet);
	    }
    }
}
