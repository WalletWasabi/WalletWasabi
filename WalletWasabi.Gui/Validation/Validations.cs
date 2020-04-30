using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Validation
{
	public class Validations : ReactiveObject, IRegisterValidationMethod, IValidations
	{
		private Dictionary<string, ErrorDescriptors> _errorsByPropertyName;
		private Dictionary<string, ValidateMethod> _validationMethods;

		public Validations()
		{
			_errorsByPropertyName = new Dictionary<string, ErrorDescriptors>();
			_validationMethods = new Dictionary<string, ValidateMethod>();
		}

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public bool Any => _errorsByPropertyName.Where(x => x.Value.Any()).Any();

		public bool AnyErrors => _errorsByPropertyName.Where(x => x.Value.Any(x => x.Severity == ErrorSeverity.Error)).Any();

		public bool AnyWarnings => _errorsByPropertyName.Where(x => x.Value.Any(x => x.Severity == ErrorSeverity.Warning)).Any();

		public bool AnyInfos => _errorsByPropertyName.Where(x => x.Value.Any(x => x.Severity == ErrorSeverity.Info)).Any();

		IEnumerable<string> IValidations.Infos => _errorsByPropertyName.Values.SelectMany(x => x.Where(x => x.Severity == ErrorSeverity.Info).Select(x => x.Message));

		IEnumerable<string> IValidations.Warnings => _errorsByPropertyName.Values.SelectMany(x => x.Where(x => x.Severity == ErrorSeverity.Warning).Select(x => x.Message));

		IEnumerable<string> IValidations.Errors => _errorsByPropertyName.Values.SelectMany(x => x.Where(x => x.Severity == ErrorSeverity.Error).Select(x => x.Message));

		public void Validate()
		{
			foreach (var propertyName in _validationMethods.Keys)
			{
				ValidateProperty(propertyName);
			}
		}

		public void ValidateProperty(string propertyName)
		{
			if (_validationMethods.ContainsKey(propertyName))
			{
				ClearErrors(propertyName);

				var del = _validationMethods[propertyName];

				var method = del as ValidateMethod;

				method(_errorsByPropertyName[propertyName]);

				OnErrorsChanged(propertyName);
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

		public IEnumerable GetErrors(string propertyName)
		{
			return _errorsByPropertyName.ContainsKey(propertyName) && _errorsByPropertyName[propertyName].Any()
				? _errorsByPropertyName[propertyName]
				: ErrorDescriptors.Empty;
		}

		private void ClearErrors(string propertyName)
		{
			if (_errorsByPropertyName.ContainsKey(propertyName))
			{
				_errorsByPropertyName[propertyName].Clear();

				OnErrorsChanged(propertyName);
			}
		}

		private void OnErrorsChanged(string propertyName)
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));

			this.RaisePropertyChanged(nameof(Any));
			this.RaisePropertyChanged(nameof(AnyErrors));
			this.RaisePropertyChanged(nameof(AnyWarnings));
			this.RaisePropertyChanged(nameof(AnyInfos));
		}
	}
}
