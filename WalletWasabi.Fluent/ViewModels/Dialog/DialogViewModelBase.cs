using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	/// <summary>
	/// Foundational class for <see cref="DialogViewModelBase{TResult}"/>.
	/// Don't use this class directly since it doesn't provide the
	/// functionality required for Dialogs.
	/// </summary>	
	public abstract class DialogViewModelBase : ReactiveObject
	{
		public DialogViewModelBase(IDialogHost dialogHost)
		{
			DialogHost = dialogHost;
		}

		/// <summary>
		/// An instance of <see cref="IDialogHost"/> that owns this dialog.
		/// </summary>
		public IDialogHost DialogHost { get; }
	}
}
