using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	public abstract class AuthorizationDialogBase : DialogViewModelBase<bool>
	{
		protected AuthorizationDialogBase()
		{
			BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var result = await Authorize();
				Close(DialogResultKind.Normal, result);
			});

			EnableAutoBusyOn(NextCommand);
		}

		protected abstract Task<bool> Authorize();
	}
}
