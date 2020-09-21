using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.News
{
	public class ClientUpdates
	{
		public ClientUpdates(IEnumerable<UpdateItem> updates)
		{
			Updates = updates;
		}

		public IEnumerable<UpdateItem> Updates { get; }
	}
}
