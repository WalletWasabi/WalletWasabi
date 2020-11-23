using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class EnterPasswordViewModel : DialogViewModelBase<string?>
	{
		private string? _confirmPassword;
		private string? _password;

		public EnterPasswordViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, string subtitle) : base(navigationState, navigationTarget)
		{
			Subtitle = subtitle;

			// This means pressing continue will make the password empty string.
			// pressing cancel will return null.
			_password = "";

			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.ConfirmPassword, ValidateConfirmPassword);

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

					return IsDialogOpen && ((string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(ConfirmPassword)) || (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword) && !Validations.Any));
				})
				.ObserveOn(RxApp.MainThreadScheduler);

			var cancelCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(() => Close(), backCommandCanExecute);
			NextCommand = ReactiveCommand.Create(() => Close(Password), nextCommandCanExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(), cancelCommandCanExecute);
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

		public ICommand NextCommand { get; }

		public string Subtitle { get; }

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
}