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
	public class ViewModelBase : ReactiveObject
	{		
		private Dictionary<string, ErrorDescriptors> _errorsByPropertyName;
		private Dictionary<string, ValidateMethod> _validationMethods;

		public ViewModelBase()
		{
			_errorsByPropertyName = new Dictionary<string, ErrorDescriptors>();
			_validationMethods = new Dictionary<string, ValidateMethod>();

			PropertyChanged += ViewModelBase_PropertyChanged;
		}		

		protected void RegisterValidationMethod(string propertyName, ValidateMethod validateMethod)
		{
			if (string.IsNullOrWhiteSpace(propertyName))
			{
				throw new ArgumentException("PropertyName must be valid.", nameof(propertyName));
			}

			_validationMethods[propertyName] = validateMethod;
		}

		private void ViewModelBase_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(e.PropertyName))
			{
				foreach (var propertyName in _validationMethods.Keys)
				{
					ValidateProperty(propertyName);
				}
			}
			else
			{
				ValidateProperty(e.PropertyName);
			}
		}

		private void ValidateProperty(string propertyName)
		{
			if (_validationMethods.ContainsKey(propertyName))
			{
				ClearErrors(propertyName);

				_validationMethods[propertyName]((severity, error) =>
				{
					AddError(propertyName, severity, error);
				});
			}
		}

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public bool HasErrors => _errorsByPropertyName.Where(x => x.Value.HasErrors).Any();		

		public IEnumerable GetErrors(string propertyName)
		{
			return _errorsByPropertyName.ContainsKey(propertyName) && _errorsByPropertyName[propertyName].HasErrors
				? _errorsByPropertyName[propertyName]
				: ErrorDescriptors.Empty;
		}

		private void AddError(string propertyName, ErrorSeverity severity, string error)
		{
			if (!_errorsByPropertyName.ContainsKey(propertyName))
			{
				_errorsByPropertyName[propertyName] = new ErrorDescriptors();
			}

			_errorsByPropertyName[propertyName].Add(new ErrorDescriptor(severity, error));

			OnErrorsChanged(propertyName);

			//this.RaisePropertyChanged(nameof(HasErrors));
		}

		private void ClearErrors(string propertyName)
		{
			if (_errorsByPropertyName.ContainsKey(propertyName))
			{
				_errorsByPropertyName.Remove(propertyName);

				OnErrorsChanged(propertyName);

				//this.RaisePropertyChanged(nameof(HasErrors));
			}
		}

		private void OnErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}
