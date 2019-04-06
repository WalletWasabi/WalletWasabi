using System;
using ReactiveUI;

namespace WalletWasabi.Gui.ViewModels
{
	public class Notification
	{
		public Notification(NotificationTypeEnum notificationType, string notificationText, bool unattended, bool duplicated)
		{
			NotificationType = notificationType;
			NotificationText = notificationText;
			Unattended = unattended;
			Duplicated = duplicated;
		}

		public NotificationTypeEnum NotificationType { get; }
		public string NotificationText { get; }
		public bool Unattended { get; }
		public bool Duplicated { get; }
	}

	public class NotificationViewModel : ViewModelBase
	{
		public ReactiveCommand CloseCommand { get; } 

		private string _notificationText;
		public string NotificationText
		{
			get => _notificationText;
			set => this.RaiseAndSetIfChanged(ref _notificationText, value);
		}

		private NotificationTypeEnum _notificationType;

		public NotificationTypeEnum NotificationType
		{
			get => _notificationType;
			set => this.RaiseAndSetIfChanged(ref _notificationType, value);
		}

		private bool _displaying;

		public bool Displaying
		{
			get => _displaying;
			set 
			{
				this.RaiseAndSetIfChanged(ref _displaying, value);
				if(_displaying == false)
					_container.Notifications.Remove(this);
			}
		}

		private NotificationBarViewModel _container;

		public NotificationViewModel(NotificationBarViewModel container)
		{
			CloseCommand = ReactiveCommand.Create(()=>Displaying = false);
			_container = container;
		}

		internal void IncreaseCounter()
		{
			var str = NotificationText; 
			if(!str.StartsWith("["))
			{
				NotificationText = "[2] " + str; 
			}
			else
			{
				var i = str.IndexOf(']');
				var number = int.Parse(str.Substring(1, i-1));
				NotificationText = $"[{number +1}]" + str.Substring(i+1);
			}
		}
	}
}
