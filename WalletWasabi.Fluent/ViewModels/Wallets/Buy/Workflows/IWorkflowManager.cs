namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowManager
{
	IWorkflowValidator WorkflowValidator { get; }

	WorkflowViewModel? CurrentWorkflow { get; }

	void SendApiRequest();

	void SelectNextWorkflow();
}
