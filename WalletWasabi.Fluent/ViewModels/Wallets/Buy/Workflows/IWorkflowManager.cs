using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowManager
{
	IWorkflowValidator WorkflowValidator { get; }

	WorkflowViewModel? CurrentWorkflow { get; }

	Task SendApiRequestAsync(CancellationToken cancellationToken);

	void SelectNextWorkflow();
}
