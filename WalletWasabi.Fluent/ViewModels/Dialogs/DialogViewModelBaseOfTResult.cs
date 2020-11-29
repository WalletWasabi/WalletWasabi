using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;

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
		private readonly IDisposable _disposable;
		private readonly TaskCompletionSource<TResult> _currentTaskCompletionSource;

		protected DialogViewModelBase()
		{
			_currentTaskCompletionSource = new TaskCompletionSource<TResult>();

			_disposable = this.WhenAnyValue(x => x.IsDialogOpen)
							  .Skip(1) // Skip the initial value change (which is false).
							  .DistinctUntilChanged()
							  .Subscribe(OnIsDialogOpenChanged);

			CancelCommand = ReactiveCommand.Create(() => Close());
		}

		protected override void OnNavigatedFrom()
		{
			Close();

			base.OnNavigatedFrom();
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
			if (_currentTaskCompletionSource.Task.IsCompleted)
			{
				return;
			}

			_currentTaskCompletionSource.SetResult(value);

			_disposable.Dispose();

			IsDialogOpen = false;

			OnDialogClosed();
		}

		/// <summary>
		/// Shows the dialog.
		/// </summary>
		/// <returns>The value to be returned when the dialog is finished.</returns>
		public Task<TResult> ShowDialogAsync(IDialogHost? host = null)
		{
			if (host is null)
			{
				host = MainViewModel.Instance;
			}

			if (host is { })
			{
				host.CurrentDialog = this;
			}

			IsDialogOpen = true;

			return _currentTaskCompletionSource.Task;
		}

		/// <summary>
		/// Gets the dialog result.
		/// </summary>
		/// <returns>The value to be returned when the dialog is finished.</returns>
		public Task<TResult> GetDialogResultAsync()
		{
			IsDialogOpen = true;

			return _currentTaskCompletionSource.Task;
		}

		/// <summary>
		/// Method that is triggered when the dialog is closed.
		/// </summary>
		protected abstract void OnDialogClosed();
	}
}