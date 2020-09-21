using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.News
{
	public class UpdateItem
	{
		public UpdateItem(Date date, string title, string description, Uri link)
		{
			Date = date;
			Title = title;
			Description = description;
			Link = link;
		}

		public Date Date { get; }
		public string Title { get; }
		public string Description { get; }
		public Uri Link { get; }
	}
}
