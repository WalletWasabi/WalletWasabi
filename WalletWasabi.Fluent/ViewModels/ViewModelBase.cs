using ReactiveUI;
using System.Collections;
using System.ComponentModel;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Validation;

namespace WalletWasabi.Fluent.ViewModels;

public class ViewModelBase : ReactiveObject, INotifyDataErrorInfo, IRegisterValidationMethod
{
	private Validations _validations;

	public ViewModelBase()
	{
		_validations = new Validations();
		_validations.ErrorsChanged += OnValidations_ErrorsChanged;
		PropertyChanged += ViewModelBase_PropertyChanged;
	}

	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

	protected IValidations Validations => _validations;

	bool INotifyDataErrorInfo.HasErrors => Validations.Any;

	protected UiContext UiContext { get; set; } = UiContext.Default;

	protected void ClearValidations()
	{
		_validations.Clear();
	}

	private void OnValidations_ErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
	{
		ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(e.PropertyName));
	}

	private void ViewModelBase_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(e.PropertyName))
		{
			_validations.Validate();
		}
		else
		{
			_validations.ValidateProperty(e.PropertyName);
		}
	}

	IEnumerable INotifyDataErrorInfo.GetErrors(string? propertyName)
	{
		return _validations.GetErrors(propertyName);
	}

	void IRegisterValidationMethod.RegisterValidationMethod(string propertyName, ValidateMethod validateMethod)
	{
		((IRegisterValidationMethod)_validations).RegisterValidationMethod(propertyName, validateMethod);
	}
}
