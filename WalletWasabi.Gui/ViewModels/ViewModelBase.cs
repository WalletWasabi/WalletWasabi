using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.ViewModels
{
	public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo, IRegisterValidationMethod
	{
		private Dictionary<string, ErrorDescriptors> _errorsByPropertyName;
		private Dictionary<string, ValidateMethod> _validationMethods;

		public ViewModelBase()
		{
			_errorsByPropertyName = new Dictionary<string, ErrorDescriptors>();
			_validationMethods = new Dictionary<string, ValidateMethod>();

			PropertyChanged += ViewModelBase_PropertyChanged;
		}

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public bool HasErrors => _errorsByPropertyName.Where(x => x.Value.HasErrors).Any();

		private static IEnumerable<MethodInfo> GetValidateMethods(Type type)
		{
			if (type.BaseType != null)
			{
				foreach (var method in GetValidateMethods(type.BaseType))
				{
					yield return method;
				}
			}

			foreach (var method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
				.Where(x => x.Name.StartsWith("Validate")))
			{
				yield return method;
			}
		}

		void IRegisterValidationMethod.RegisterValidationMethod(string propertyName, ValidateMethod validateMethod)
		{
			if (string.IsNullOrWhiteSpace(propertyName))
			{
				throw new ArgumentException("PropertyName must be valid.", nameof(propertyName));
			}

			_validationMethods[propertyName] = validateMethod;
			_errorsByPropertyName[propertyName] = ErrorDescriptors.Create();
		}

		protected void Validate()
		{
			foreach (var propertyName in _validationMethods.Keys)
			{
				DoValidateProperty(propertyName);
			}
		}

		private void ViewModelBase_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(e.PropertyName))
			{
				Validate();
			}
			else
			{
				DoValidateProperty(e.PropertyName);
			}
		}

		private void DoValidateProperty(string propertyName)
		{
			if (_validationMethods.ContainsKey(propertyName))
			{
				ClearErrors(propertyName);

				var del = _validationMethods[propertyName];

				var method = del as ValidateMethod;

				method(_errorsByPropertyName[propertyName]);

				OnErrorsChanged(propertyName);

				this.RaisePropertyChanged(nameof(HasErrors));
			}
		}

		public IEnumerable GetErrors(string propertyName)
		{
			return _errorsByPropertyName.ContainsKey(propertyName) && _errorsByPropertyName[propertyName].HasErrors
				? _errorsByPropertyName[propertyName]
				: ErrorDescriptors.Empty;
		}

		private void ClearErrors(string propertyName)
		{
			if (_errorsByPropertyName.ContainsKey(propertyName))
			{
				_errorsByPropertyName[propertyName].Clear();

				OnErrorsChanged(propertyName);

				this.RaisePropertyChanged(nameof(HasErrors));
			}
		}

		private void OnErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
		}
	}
}
