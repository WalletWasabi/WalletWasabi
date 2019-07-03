using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
			if (string.IsNullOrEmpty(errorString))
			{
				return null;
			}
			else
			{
				return new List<string> { errorString };
			}
		}

		protected void NotifyErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}
