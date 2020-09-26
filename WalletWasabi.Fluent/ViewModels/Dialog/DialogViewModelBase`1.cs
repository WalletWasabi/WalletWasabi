using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	public abstract class DialogViewModelBase<TResult> : DialogViewModelBase
	{
		public abstract void DialogShown(TaskCompletionSource<TResult> tcs);

		public DialogViewModelBase(MainViewModel mainViewModel) : base(mainViewModel)
		{

		}

		public Task<TResult> ShowDialogAsync()
		{
			var tcs = new TaskCompletionSource<TResult>();
			
			DialogHost.ShowDialog(this);
			DialogShown(tcs);

			return tcs.Task;
		}
	}
}
