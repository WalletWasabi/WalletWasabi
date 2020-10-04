using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	/// <summary>
	/// Base ViewModel class for Dialogs that returns a value back.
	/// </summary>
	/// <typeparam name
	="TResult">The type of the value to be returned when the dialog is finished.</typeparam>
	public abstract class DialogViewModelBase<TResult> : DialogViewModelBase
	{
		private TaskCompletionSource<TResult>? _tcs { get; set; } 

		public DialogViewModelBase(MainViewModel mainViewModel) : base(mainViewModel)
		{
		}

		/// <summary>
		/// Method to be called when the dialog intends to close
		/// and ready to pass a value back to the caller.
		/// </summary>
		/// <param name="value">The return value of the dialog</param>
		public void CloseDialog(TResult value)
		{
			_tcs.SetResult(value);
			_tcs = null;
		}

		/// <summary>
		/// Shows the dialog.
		/// </summary>
		/// <returns>The value to be returned when the dialog is finished.</returns>
		public Task<TResult> ShowDialogAsync()
		{
			_tcs = new TaskCompletionSource<TResult>();

			DialogHost.ShowDialog(this);
			DialogShown();

			return _tcs.Task;
		}

		/// <summary>
		/// Method that is triggered when the dialog
		/// is to be shown.
		/// </summary>
		protected abstract void DialogShown();
	}
}
