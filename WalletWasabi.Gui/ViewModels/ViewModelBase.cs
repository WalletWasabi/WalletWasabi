using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.ViewModels
{
	public delegate void ValidateMethod(IValidationErrors errors);

	public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo
	{
		private Dictionary<string, ErrorDescriptors> _errorsByPropertyName;
		private Dictionary<string, ValidateMethod> _validationMethods;

		public ViewModelBase()
		{
			_errorsByPropertyName = new Dictionary<string, ErrorDescriptors>();
			_validationMethods = new Dictionary<string, ValidateMethod>();

			RegisterValidationMethods();

			PropertyChanged += ViewModelBase_PropertyChanged;
		}

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public bool HasErrors => _errorsByPropertyName.Where(x => x.Value.HasErrors).Any();

		private static bool IsValidationMethod(MethodInfo methodInfo)
		{
			var parameters = methodInfo.GetParameters();

			if (parameters.Length == 1)
			{
				if (parameters.First().ParameterType == typeof(IValidationErrors))
				{
					return true;
				}
			}

			return false;
		}

		private void RegisterValidationMethods()
		{
			var type = GetType();
			var methods = GetValidateMethods(type);

			var validateMethods =
				methods.Where(x =>
				x.Name.StartsWith("Validate") &&
				x.Name.Length > 8 &&
				IsValidationMethod(x))
				.ToList();

			foreach (var validateMethod in validateMethods)
			{
				var propertyName = validateMethod.Name.Remove(0, 8);
				var property = type.GetProperty(propertyName);

				if (property != null)
				{
					var del = validateMethod.CreateDelegate(typeof(ValidateMethod), this) as ValidateMethod;

					if (del != null)
					{
						RegisterValidationMethod(propertyName, del);
					}
				}
			}
		}

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

		private void RegisterValidationMethod(string propertyName, ValidateMethod validateMethod)
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
