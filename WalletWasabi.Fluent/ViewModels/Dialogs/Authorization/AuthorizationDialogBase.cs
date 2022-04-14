using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

public abstract partial class AuthorizationDialogBase : DialogViewModelBase<bool>
{
	[AutoNotify] private bool _hasAuthorizationFailed;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private string _authorizationFailedMessage = "The Authorization has failed, please try again.";

	protected AuthorizationDialogBase()
	{
		NextCommand = ReactiveCommand.CreateFromTask(AuthorizeCoreAsync);

		EnableAutoBusyOn(NextCommand);
	}

	protected abstract Task<bool> AuthorizeAsync();

	private async Task AuthorizeCoreAsync()
	{
		HasAuthorizationFailed = !await AuthorizeAsync();

		if (!HasAuthorizationFailed)
		{
			Close(DialogResultKind.Normal, true);
		}
	}
}
