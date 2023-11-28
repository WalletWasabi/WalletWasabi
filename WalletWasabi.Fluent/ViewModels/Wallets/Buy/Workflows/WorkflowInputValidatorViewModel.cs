using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class WorkflowInputValidatorViewModel : ReactiveObject
{
	public abstract bool IsValid(string message);
}
