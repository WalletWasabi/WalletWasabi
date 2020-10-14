using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
    /// <summary>
    /// Foundational class for <see cref="DialogViewModelBase{TResult}"/>.
    /// Don't use this class directly since it doesn't provide the
    /// functionality required for Dialogs.
    /// </summary>	
    public abstract class DialogViewModelBase : ViewModelBase
    { 
        private bool _isDialogOpen;
        private bool _dialogReturnedWithValue;

        /// <summary>
        /// Gets or sets if the dialog is opened/closed.
        /// </summary>
        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
        }

        /// <summary>
        /// Gets or sets if the dialog returned with a value or not.
        /// </summary>
        public bool DialogReturnedWithValue
        {
            get => _dialogReturnedWithValue;
            set => this.RaiseAndSetIfChanged(ref _dialogReturnedWithValue, value);
        }

        /// <summary>
        /// Method that is triggered when the dialog
        /// is about to close.
        /// </summary>
        protected abstract void OnDialogClosed();
    }
}