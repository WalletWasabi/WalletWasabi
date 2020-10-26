using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	/// <summary>
	/// Base ViewModel class for Dialogs that returns a value back.
	/// Do not reuse all types derived from this after calling ShowDialogAsync.
	/// Spawn a new instance instead after that.
	/// </summary>
	/// <typeparam name="TResult">The type of the value to be returned when the dialog is finished.</typeparam>
	public abstract class DialogViewModelBase<TResult> : DialogViewModelBase
	{
		private readonly IDisposable Disposable;
		private readonly TaskCompletionSource<TResult> CurrentTaskCompletionSource;
		private bool _isDialogOpen;

		protected DialogViewModelBase()
		{
			CurrentTaskCompletionSource = new TaskCompletionSource<TResult>();

			Disposable = this.WhenAnyValue(x => x.IsDialogOpen)
							  .Skip(1) // Skip the initial value change (which is false).
							  .DistinctUntilChanged()
							  .Subscribe(OnIsDialogOpenChanged);
		}

		/// <summary>
		/// Gets or sets if the dialog is opened/closed.
		/// </summary>
		public bool IsDialogOpen
		{
			get => _isDialogOpen;
			set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
		}

		private void OnIsDialogOpenChanged(bool dialogState)
		{
			// Triggered when closed abruptly (via the dialog overlay or the back button).
			if (!dialogState)
			{
				Close();
			}
		}

		/// <summary>
		/// Method to be called when the dialog intends to close
		/// and ready to pass a value back to the caller.
		/// </summary>
		/// <param name="value">The return value of the dialog</param>
		protected void Close(TResult value = default)
		{
			if (CurrentTaskCompletionSource.Task.IsCompleted)
			{
				throw new InvalidOperationException("Dialog is already closed.");
			}

			CurrentTaskCompletionSource.SetResult(value);

			Disposable.Dispose();

			IsDialogOpen = false;

			OnDialogClosed();
		}

		/// <summary>
		/// Shows the dialog.
		/// </summary>
		/// <returns>The value to be returned when the dialog is finished.</returns>
		public Task<TResult> ShowDialogAsync(IDialogHost host)
		{
			host.CurrentDialog = this;
			IsDialogOpen = true;

			return CurrentTaskCompletionSource.Task;
		}

		/// <summary>
		/// Method that is triggered when the dialog is closed.
		/// </summary>
		protected abstract void OnDialogClosed();
	}
}
