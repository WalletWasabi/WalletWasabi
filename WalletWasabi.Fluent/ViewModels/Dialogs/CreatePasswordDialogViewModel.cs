using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CreatePasswordDialogViewModel : DialogViewModelBase<string?>
{
	[AutoNotify] private string? _confirmPassword;
	[AutoNotify] private string? _password;

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

		var backCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

		var nextCommandCanExecute = this.WhenAnyValue(
				x => x.IsDialogOpen,
				x => x.Password,
				x => x.ConfirmPassword,
				delegate
				{
					// This will fire validations before return canExecute value.
					this.RaisePropertyChanged(nameof(Password));
					this.RaisePropertyChanged(nameof(ConfirmPassword));

					return IsDialogOpen &&
						   ((enableEmpty && string.IsNullOrEmpty(Password) &&
							 string.IsNullOrEmpty(ConfirmPassword)) ||
							(!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword) &&
							 !Validations.Any));
				})
			.ObserveOn(RxApp.MainThreadScheduler);

		var cancelCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

		BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back), backCommandCanExecute);
		NextCommand = ReactiveCommand.Create(() => Close(result: Password), nextCommandCanExecute);
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel), cancelCommandCanExecute);
	}

	public sealed override string Title { get; protected set; }

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
