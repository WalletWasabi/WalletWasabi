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

        /// <summary>
        /// Gets or sets if the dialog is opened/closed.
        /// </summary>
        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
        }

        /// <summary>
        /// Method that is triggered when the dialog is closed.
        /// </summary>
        protected abstract void OnDialogClosed();
    }
}