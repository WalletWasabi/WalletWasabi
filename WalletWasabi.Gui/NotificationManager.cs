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

		public void Info(string notificationText)    => Notify(NotificationTypeEnum.Info, notificationText, false);
		public void Success(string notificationText) => Notify(NotificationTypeEnum.Success, notificationText, false);
		public void Warning(string notificationText) => Notify(NotificationTypeEnum.Warning, notificationText, false);
		public void Error(string notificationText)   => Notify(NotificationTypeEnum.Error, notificationText, false);

		public void InfoUnattended(string notificationText)    => Notify(NotificationTypeEnum.Info, notificationText, true);
		public void SuccessUnattended(string notificationText) => Notify(NotificationTypeEnum.Success, notificationText, true);
		public void WarningUnattended(string notificationText) => Notify(NotificationTypeEnum.Warning, notificationText, true);
		public void ErrorUnattended(string notificationText)   => Notify(NotificationTypeEnum.Error, notificationText, true);

		private void Notify(NotificationTypeEnum notificationType, string notificationText, bool unattended)
		{
			var notificationId = Hashes.Hash256(Encoding.UTF8.GetBytes(notificationText));
			if(notificationId == _lastNotificationId && (DateTime.UtcNow-_lastNotificationTime) < TimeSpan.FromSeconds(1) ) 
				return;
			
			NotificationAvailable?.Invoke(this, new Notification(notificationType, notificationText, unattended));
			_lastNotificationId = notificationId;
			_lastNotificationTime = DateTime.UtcNow;
		}
	}
}
