using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
    public class AdvancedRecoveryOptionsViewModel : DialogViewModelBase<(string? keypath, string? gaplimit)>
    {
        private string? _accountKeyPath;
        private string? _minGapLimit;

        public AdvancedRecoveryOptionsViewModel()
        {
            this.ValidateProperty(x => x.AccountKeyPath, ValidateAccountKeyPath);
            this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);

            var continueCommandCanExecute = this.WhenAnyValue(
                    x => x.AccountKeyPath,
                    x => x.MinGapLimit,
                    (keyPath, minGapLimit) =>
                    {
                        // This will fire validations before return canExecute value.
                        this.RaisePropertyChanged(nameof(AccountKeyPath));
                        this.RaisePropertyChanged(nameof(MinGapLimit));

                        return string.IsNullOrEmpty(keyPath) && string.IsNullOrEmpty(minGapLimit) ||
                               !string.IsNullOrEmpty(keyPath) && !string.IsNullOrEmpty(minGapLimit) && !Validations.Any;
                    })
                .ObserveOn(RxApp.MainThreadScheduler);

            ContinueCommand = ReactiveCommand.Create(() => Close((AccountKeyPath, MinGapLimit)), continueCommandCanExecute);
            CancelCommand = ReactiveCommand.Create(() => Close());
        }

        protected override void OnDialogClosed()
        {
            AccountKeyPath = "";
            MinGapLimit = "";
        }

        private void ValidateMinGapLimit(IValidationErrors errors)
        {
            if (string.IsNullOrWhiteSpace(MinGapLimit))
            {
                return;
            }
            
            if (!int.TryParse(MinGapLimit, out var minGapLimit) || minGapLimit < KeyManager.AbsoluteMinGapLimit ||
                minGapLimit > KeyManager.MaxGapLimit)
            {
                errors.Add(ErrorSeverity.Error,
                    $"Must be a number between {KeyManager.AbsoluteMinGapLimit} and {KeyManager.MaxGapLimit}.");
            }
        }

        private void ValidateAccountKeyPath(IValidationErrors errors)
        {
            if (string.IsNullOrWhiteSpace(AccountKeyPath))
            {
                return;
            }

            if (KeyPath.TryParse(AccountKeyPath, out var keyPath) && keyPath is { })
            {
                var accountKeyPath = keyPath.GetAccountKeyPath();
                if (keyPath.Length != accountKeyPath.Length ||
                    accountKeyPath.Length != KeyManager.DefaultAccountKeyPath.Length)
                {
                    errors.Add(ErrorSeverity.Error, "Path is not a compatible account derivation path.");
                }
            }
            else
            {
                errors.Add(ErrorSeverity.Error, "Path is not a valid derivation path.");
            }
        }

        public string? AccountKeyPath
        {
            get => _accountKeyPath;
            set => this.RaiseAndSetIfChanged(ref _accountKeyPath, value);
        }

        public string? MinGapLimit
        {
            get => _minGapLimit;
            set => this.RaiseAndSetIfChanged(ref _minGapLimit, value);
        }

        public ICommand ContinueCommand { get; }

        public ICommand CancelCommand { get; }
    }
}