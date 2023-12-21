using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Validation;

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

	public bool Any => ErrorsByPropertyName.Any(x => x.Value.Any());

	public bool AnyErrors => ErrorsByPropertyName.Any(x => x.Value.Any(error => error.Severity == ErrorSeverity.Error));

	public bool AnyWarnings => ErrorsByPropertyName.Any(x => x.Value.Any(error => error.Severity == ErrorSeverity.Warning));

	public bool AnyInfoItems => ErrorsByPropertyName.Any(x => x.Value.Any(error => error.Severity == ErrorSeverity.Info));

	IEnumerable<string> IValidations.InfoItems => ErrorsByPropertyName.Values.SelectMany(x => x.Where(error => error.Severity == ErrorSeverity.Info).Select(error => error.Message));

	IEnumerable<string> IValidations.Warnings => ErrorsByPropertyName.Values.SelectMany(x => x.Where(error => error.Severity == ErrorSeverity.Warning).Select(error => error.Message));

	IEnumerable<string> IValidations.Errors => ErrorsByPropertyName.Values.SelectMany(x => x.Where(error => error.Severity == ErrorSeverity.Error).Select(error => error.Message));

	public void Clear()
	{
		foreach (var propertyName in ValidationMethods.Keys)
		{
			ValidateProperty(propertyName, true);
			ErrorsByPropertyName[propertyName].Clear();
		}
	}

	public void Validate()
	{
		foreach (var propertyName in ValidationMethods.Keys)
		{
			ValidateProperty(propertyName);
		}
	}

	public void ValidateProperty(string propertyName, bool clear = false)
	{
		if (ValidationMethods.TryGetValue(propertyName, out ValidateMethod? validationMethod))
		{
			var currentErrors = ErrorsByPropertyName[propertyName];

			// Copy the current errors.
			var previousErrors = currentErrors.ToList();

			if (!clear)
			{
				// Validate.
				validationMethod(currentErrors);
			}

			// Clear obsoleted errors and notify properties that changed.
			UpdateAndNotify(currentErrors, previousErrors, propertyName);
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

	public IEnumerable GetErrors(string? propertyName)
	{
		if (!string.IsNullOrWhiteSpace(propertyName))
		{
			return ErrorsByPropertyName.TryGetValue(propertyName, out ErrorDescriptors? value) && value.Any()
				? value : ErrorDescriptors.Empty;
		}
		else
		{
			return ErrorDescriptors.Empty;
		}
	}

	private void UpdateAndNotify(List<ErrorDescriptor> currentErrors, List<ErrorDescriptor> previousErrors, string propertyName)
	{
		// Severities of the new errors.
		var categoriesToNotify = currentErrors.Except(previousErrors).Select(x => x.Severity).Distinct().ToList();

		// Remove the old errors.
		previousErrors.ForEach(x => currentErrors.Remove(x));

		// Severities of the obsoleted errors.
		categoriesToNotify.AddRange(previousErrors.Except(currentErrors).Select(x => x.Severity).Distinct().ToList());

		OnErrorsChanged(propertyName, categoriesToNotify);
	}

	private void OnErrorsChanged(string propertyName, List<ErrorSeverity> categoriesToNotify)
	{
		static string Selector(ErrorSeverity x) => x switch
		{
			ErrorSeverity.Info => nameof(AnyInfoItems),
			ErrorSeverity.Warning => nameof(AnyWarnings),
			ErrorSeverity.Error => nameof(AnyErrors),
			_ => throw new NotImplementedException(),
		};

		var propertiesToNotify = categoriesToNotify.Select(Selector).ToList();

		if (propertiesToNotify.Any())
		{
			ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
			this.RaisePropertyChanged(nameof(Any));
		}

		propertiesToNotify.ForEach(this.RaisePropertyChanged);
	}
}
