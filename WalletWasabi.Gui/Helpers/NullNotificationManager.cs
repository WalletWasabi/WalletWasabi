using Avalonia.Controls.Notifications;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Helpers
{
	public class NullNotificationManager : INotificationManager
	{
		public void Show(INotification notification)
		{
		}
	}
}
