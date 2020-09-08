using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Tabs.Dashboard
{
	public class NewsItem
	{
		public DateTime DatePublished { get; set; }
		public string Header { get; set; }
		public string Message { get; set; }
		public string Summary { get; set; }
	}
}
