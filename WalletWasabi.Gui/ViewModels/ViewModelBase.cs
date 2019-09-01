using Avalonia.Animation;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.ViewModels
{
	public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo
	{
		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public bool HasErrors => Validator.ValidateAllProperties(this).Any();

		public IEnumerable GetErrors(string propertyName)
		{
			var errorString = Validator.ValidateProperty(this, propertyName);
			if (!string.IsNullOrEmpty(errorString))
			{
				return new List<string> { errorString };
			}

			return null;
		}

		private bool _msgWrapFirstTime = true;
		private IDisposable _msgWrapExpiryMonitorDisposable;
		private IMessageWrapper _msgWrapper;
		private readonly TimeSpan _messageExpiry = TimeSpan.FromSeconds(7);
		private DateTime _lastMessageEntry = DateTime.UtcNow;

		public IMessageWrapper WrappedMessage
		{
			get => _msgWrapper;
			set
			{
				if (_msgWrapFirstTime)
				{
					_msgWrapExpiryMonitorDisposable = Clock.GlobalClock
															.Where(x => _msgWrapper != null)
															.Subscribe(MsgWrapExpiryMonitor);
					_msgWrapFirstTime = false;
				}

				_lastMessageEntry = DateTime.UtcNow;

				this.RaiseAndSetIfChanged(ref _msgWrapper, value);
			}
		}

		private void MsgWrapExpiryMonitor(TimeSpan obj)
		{
			if (_lastMessageEntry + _messageExpiry < DateTime.UtcNow)
			{
				WrappedMessage = null;
			}
		}

		public void SetWarningMessage(object message)
		{
			if (message is string & string.IsNullOrWhiteSpace(message as string))
			{
				return;
			}

			WrappedMessage = new WarningMessageWrapper(message);
		}

		public void SetSuccessMessage(object message)
		{
			if (message is string & string.IsNullOrWhiteSpace(message as string))
			{
				return;
			}

			WrappedMessage = new SuccessMessageWrapper(message);
		}

		public void SetValidationMessage(object message)
		{
			if (message is string & string.IsNullOrWhiteSpace(message as string))
			{
				return;
			}

			WrappedMessage = new ValidationMessageWrapper(message);
		}

		protected void NotifyErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}
