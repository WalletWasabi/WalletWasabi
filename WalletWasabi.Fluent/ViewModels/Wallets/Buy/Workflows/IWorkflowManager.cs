using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowManager
{
	IWorkflowValidator WorkflowValidator { get; }

	Workflow? CurrentWorkflow { get; }

	Task SendApiRequestAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Selects next scripted workflow or use command to override.
	/// </summary>
	/// <param name="command">The remote command override to select next workflow.</param>
	/// <returns>True is next workflow selected successfully or current workflow will continue.</returns>
	bool SelectNextWorkflow(string? command);
}
