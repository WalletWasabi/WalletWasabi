using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class EnterPasswordViewModel : DialogViewModelBase<string?>
	{
		private string? _password;
		private string? _confirmPassword;

		public EnterPasswordViewModel()
		{
			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.ConfirmPassword, ValidateConfirmPassword);

			var continueCommandCanExecute = this.WhenAnyValue(
				x => x.Password,
				x => x.ConfirmPassword,
				(password, confirmPassword) =>
				{
					// This will fire validations before return canExecute value.
					this.RaisePropertyChanged(nameof(Password));
					this.RaisePropertyChanged(nameof(ConfirmPassword));

					return (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(confirmPassword)) || (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(confirmPassword) && !Validations.Any);
				})
				.ObserveOn(RxApp.MainThreadScheduler);

			ContinueCommand = ReactiveCommand.Create(() => Close(Password), continueCommandCanExecute);
			CancelCommand = ReactiveCommand.Create(() => Close());
		}

		public string? Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string? ConfirmPassword
		{
			get => _confirmPassword;
			set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
		}

		public ICommand ContinueCommand { get; }
		public ICommand CancelCommand { get; }

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
			if (PasswordHelper.IsTrimable(Password, out _))
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.WhitespaceMessage);
			}

			if (PasswordHelper.IsTooLong(Password, out _))
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage);
			}
		}
	}
}