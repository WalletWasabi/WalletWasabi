namespace WalletWasabi.Fluent.ViewModels.Dialog
{
    /// <summary>
    /// Interface that abstracts <see cref="DialogViewModelBase{TResult}"/>.
    /// </summary>
    public interface IDialogViewModel
    {
        /// <summary>
        /// Gets or sets if the dialog is opened/closed.
        /// </summary>
        bool IsDialogOpen { get; set; }
    }
}
