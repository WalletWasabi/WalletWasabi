using System;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui
{
	public class NotificationManager
	{
		public event EventHandler<Notification> NotificationAvailable;
		private uint256 _lastNotificationId = uint256.Zero;
		private DateTime _lastNotificationTime = DateTime.UtcNow;

		public void Notify(NotificationTypeEnum notificationType, string notificationText)
		{
			var notificationId = Hashes.Hash256(Encoding.UTF8.GetBytes(notificationText));
			if(notificationId == _lastNotificationId && (DateTime.UtcNow-_lastNotificationTime) < TimeSpan.FromSeconds(1) ) 
				return;
			
			NotificationAvailable?.Invoke(this, new Notification(notificationType, notificationText));
			_lastNotificationId = notificationId;
			_lastNotificationTime = DateTime.UtcNow;
		}
	}
}
