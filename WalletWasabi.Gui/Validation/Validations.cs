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
		public Validations()
		{
			ErrorsByPropertyName = new Dictionary<string, ErrorDescriptors>();
			ValidationMethods = new Dictionary<string, ValidateMethod>();
		}

		public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

		private Dictionary<string, ErrorDescriptors> ErrorsByPropertyName { get; }

		private Dictionary<string, ValidateMethod> ValidationMethods { get; }

		public bool Any => ErrorsByPropertyName.Where(x => x.Value.Any()).Any();

		public bool AnyErrors => ErrorsByPropertyName.Where(x => x.Value.Any(x => x.Severity == ErrorSeverity.Error)).Any();

		public bool AnyWarnings => ErrorsByPropertyName.Where(x => x.Value.Any(x => x.Severity == ErrorSeverity.Warning)).Any();

		public bool AnyInfos => ErrorsByPropertyName.Where(x => x.Value.Any(x => x.Severity == ErrorSeverity.Info)).Any();

		IEnumerable<string> IValidations.Infos => ErrorsByPropertyName.Values.SelectMany(x => x.Where(x => x.Severity == ErrorSeverity.Info).Select(x => x.Message));

		IEnumerable<string> IValidations.Warnings => ErrorsByPropertyName.Values.SelectMany(x => x.Where(x => x.Severity == ErrorSeverity.Warning).Select(x => x.Message));

		IEnumerable<string> IValidations.Errors => ErrorsByPropertyName.Values.SelectMany(x => x.Where(x => x.Severity == ErrorSeverity.Error).Select(x => x.Message));

		public void Validate()
		{
			foreach (var propertyName in ValidationMethods.Keys)
			{
				ValidateProperty(propertyName);
			}
		}

		public void ValidateProperty(string propertyName)
		{
			if (ValidationMethods.TryGetValue(propertyName, out ValidateMethod? validationMethod) && ErrorsByPropertyName.TryGetValue(propertyName, out ErrorDescriptors? currentErrors))
			{
				// Copy the current errors
				var previousErrors = currentErrors.ToList();

				// Validate
				validationMethod(currentErrors);

				// Clear obsoleted errors and notify properties that changed
				ClearAndNotify(currentErrors, previousErrors, propertyName);
			}
		}

		void IRegisterValidationMethod.RegisterValidationMethod(string propertyName, ValidateMethod validateMethod)
		{
			if (string.IsNullOrWhiteSpace(propertyName))
			{
				throw new ArgumentException("PropertyName must be valid.", nameof(propertyName));
			}

			ValidationMethods[propertyName] = validateMethod;
			ErrorsByPropertyName[propertyName] = ErrorDescriptors.Create();
		}

		public IEnumerable GetErrors(string propertyName)
		{
			return ErrorsByPropertyName.ContainsKey(propertyName) && ErrorsByPropertyName[propertyName].Any()
				? ErrorsByPropertyName[propertyName]
				: ErrorDescriptors.Empty;
		}

		private void ClearAndNotify(List<ErrorDescriptor> currentErrors, List<ErrorDescriptor> previousErrors, string propertyName)
		{
			// Severities of the new errors
			var categoriesToNotify = currentErrors.Where(x => !previousErrors.Any(y => x.Message == y.Message && x.Severity == y.Severity)).Select(x => x.Severity).ToList();

			// Remove the old errors
			previousErrors.ForEach(x => currentErrors.Remove(x));

			// Severities of the obsoleted errors
			categoriesToNotify.AddRange(previousErrors.Where(x => !currentErrors.Any(y => x.Message == y.Message && x.Severity == y.Severity)).Select(x => x.Severity).ToList());

			var propertiesToNotify = categoriesToNotify.Select(GetPropertyNameBySeverity).ToList();

			OnErrorsChanged(propertyName, propertiesToNotify);
		}

		private string GetPropertyNameBySeverity(ErrorSeverity severity) => severity switch
		{
			ErrorSeverity.Info => nameof(AnyInfos),
			ErrorSeverity.Warning => nameof(AnyWarnings),
			ErrorSeverity.Error => nameof(AnyErrors),
		};

		private void OnErrorsChanged(string propertyName, List<string> propertiesToNotify)
		{
			if (propertiesToNotify.Any())
			{
				ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
				this.RaisePropertyChanged(nameof(Any));
			}

			propertiesToNotify.ForEach(x => this.RaisePropertyChanged(x));
		}
	}
}