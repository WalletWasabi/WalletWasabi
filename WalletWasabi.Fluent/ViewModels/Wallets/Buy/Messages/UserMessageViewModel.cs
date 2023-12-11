using System.Windows.Input;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class UserMessageViewModel : MessageViewModel
{
	public UserMessageViewModel(
		ICommand? editCommand,
		IObservable<bool>? canEditObservable,
		WorkflowStep? workflowStep,
		ChatMessageMetaData metaData) : base(editCommand, canEditObservable, metaData)
	{
		WorkflowStep = workflowStep;
	}

	public WorkflowStep? WorkflowStep { get; }
}
