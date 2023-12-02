using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class UserMessageViewModel : MessageViewModel
{
	public UserMessageViewModel(
		ICommand? editCommand,
		IObservable<bool>? canEditObservable,
		WorkflowStep? workflowStep) : base(editCommand, canEditObservable)
	{
		WorkflowStep = workflowStep;
	}

	public WorkflowStep? WorkflowStep { get; }
}
