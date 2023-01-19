using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

public partial class CreatePasswordDialogViewModel : DialogViewModelBase<string?>
{
	[ObservableProperty] [NotifyPropertyChangedFor(nameof(Password))] [NotifyCanExecuteChangedFor(nameof(NextCommand))]
	private string? _confirmPassword;

	[ObservableProperty] [NotifyPropertyChangedFor(nameof(ConfirmPassword))] [NotifyCanExecuteChangedFor(nameof(NextCommand))]
	private string? _password;

	public CreatePasswordDialogViewModel(string title, string caption = "", bool enableEmpty = true)
	{
		Title = title;
		Caption = caption;

		// This means pressing continue will make the password empty string.
		// pressing cancel will return null.
		_password = "";

		this.ValidateProperty(x => x.Password, ValidatePassword);
		this.ValidateProperty(x => x.ConfirmPassword, ValidateConfirmPassword);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		BackCommand = new RelayCommand(() => Close(DialogResultKind.Back));
		NextCommand = new RelayCommand(() => Close(result: Password),
			canExecute: () => ((enableEmpty && string.IsNullOrEmpty(Password) &&
			                    string.IsNullOrEmpty(ConfirmPassword)) ||
			                   (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword) &&
			                    !Validations.Any)));
		CancelCommand = new RelayCommand(() => Close(DialogResultKind.Cancel));
	}

	public override sealed string Title { get; protected set; }

	public string Caption { get; }

	protected override void OnDialogClosed()
	{
		Password = "";
		ConfirmPassword = "";
	}

	private void ValidateConfirmPassword(IValidationErrors errors)
	{
		if (!string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword)
		{
			errors.Add(ErrorSeverity.Error, PasswordHelper.MatchingMessage);
		}
	}

	private void ValidatePassword(IValidationErrors errors)
	{
		if (PasswordHelper.IsTrimmable(Password, out _))
		{
			errors.Add(ErrorSeverity.Error, PasswordHelper.WhitespaceMessage);
		}

		if (PasswordHelper.IsTooLong(Password, out _))
		{
			errors.Add(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage);
		}
	}
}
