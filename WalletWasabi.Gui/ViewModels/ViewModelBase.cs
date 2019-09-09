using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.ViewModels
{
	public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo
	{
		private List<(string propertyName, MethodInfo mInfo)> ValidationMethodCache;
		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public ViewModelBase()
		{
			var vmc = Validator.PropertiesWithValidation(this).ToList();

			if (vmc.Count == 0) return;

			ValidationMethodCache = vmc;
		}

		public bool HasErrors => Validator.ValidateAllProperties(this, ValidationMethodCache).HasErrors;

		public IEnumerable GetErrors(string propertyName)
		{
			var error = Validator.ValidateProperty(this, propertyName, ValidationMethodCache);

			if (!error.HasErrors) return null;

			// HACK: Need to serialize this in order to pass through IndeiValidationPlugin on Avalonia 0.8.2. 
			//		 Should be removed when Avalonia has the hotfix update.
			return new List<string>() { JsonConvert.SerializeObject(error) };
		}

		protected void NotifyErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}