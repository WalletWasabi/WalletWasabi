using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

public abstract partial class AuthorizationDialogBase : DialogViewModelBase<bool>
{
	[AutoNotify] private string _errorMessage = "";

	protected AuthorizationDialogBase()
	{
		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var result = await AuthorizeCoreAsync();
			if (result)
			{
				Close(DialogResultKind.Normal, result);
			}
		});

		EnableAutoBusyOn(NextCommand);
	}

	protected abstract string AuthorizationFailedMessage { get; }

	protected abstract Task<bool> AuthorizeAsync();

	private async Task<bool> AuthorizeCoreAsync()
	{
		var success = await AuthorizeAsync();

		if (!success)
		{
			ErrorMessage = AuthorizationFailedMessage;
		}

		return success;
	}
}