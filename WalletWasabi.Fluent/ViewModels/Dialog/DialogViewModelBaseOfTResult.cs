using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
    /// <summary>
    /// Base ViewModel class for Dialogs that returns a value back.
    /// </summary>
    /// <typeparam name="TResult">The type of the value to be returned when the dialog is finished.</typeparam>
    public abstract class DialogViewModelBase<TResult> : DialogViewModelBase
    {
        private IDisposable _disposable;

        protected DialogViewModelBase()
        {
            this._disposable = this.WhenAnyValue(x => x.IsDialogOpen)
                                   .DistinctUntilChanged()
                                   .Subscribe(OnIsDialogOpenChanged);
        }

        private void OnIsDialogOpenChanged(bool obj)
        {
            // Trigger when closed abruptly (via the Overlay or the back button).
            if (!obj & CurrentTaskCompletionSource is { })
            {
                Close();
            }
        }

        private TaskCompletionSource<TResult> CurrentTaskCompletionSource { get; set; }

        /// <summary>
        /// Method to be called when the dialog intends to close
        /// and ready to pass a value back to the caller.
        /// </summary>
        /// <param name="value">The return value of the dialog</param>
        protected void Close(TResult value = default)
        {
            if (CurrentTaskCompletionSource is null)
            {
                throw new InvalidOperationException("CloseDialog with value return method failed due to missing TCS.");
            }

            CurrentTaskCompletionSource.SetResult(value);
            CurrentTaskCompletionSource = null;

            if (!(value?.Equals(default(TResult)) ?? true))
            {
                _disposable?.Dispose();
            }

            IsDialogOpen = false;

            OnDialogClosed();
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <returns>The value to be returned when the dialog is finished.</returns>
        public Task<TResult> ShowDialogAsync(IDialogHost host)
        {
            if (CurrentTaskCompletionSource is { })
            {
                throw new InvalidOperationException("Can't open a new dialog since there's already one active.");
            }

            CurrentTaskCompletionSource = new TaskCompletionSource<TResult>();

            host.CurrentDialog = this;
            IsDialogOpen = true;
            DialogReturnedWithValue = false;

            return CurrentTaskCompletionSource.Task;
        }
    }
}
