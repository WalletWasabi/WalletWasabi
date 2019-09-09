using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.ViewModels
{
	public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo
	{
		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public bool HasErrors => Validator.ValidateAllProperties(this).HasErrors;

		public IEnumerable GetErrors(string propertyName)
		{
			var error = Validator.ValidateProperty(this, propertyName);

			if (error.HasErrors)
			{
				return error;
			}

			return ErrorDescriptors.Empty;
		}

		protected void NotifyErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}
