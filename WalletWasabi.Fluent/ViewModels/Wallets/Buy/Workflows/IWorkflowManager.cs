using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowManager
{
	IWorkflowValidator WorkflowValidator { get; }

	public ConversationId Id { get; }

	Workflow? CurrentWorkflow { get; }

	Task SendChatHistoryAsync(ChatMessage[] chatMessages, CancellationToken cancellationToken);

	Task SendApiRequestAsync(ChatMessage[] chatMessages, ConversationMetaData metaData, CancellationToken cancellationToken);

	void UpdateId(ConversationId id);

	/// <summary>
	/// Selects next scripted workflow or use conversationStatus to override.
	/// </summary>
	/// <param name="conversationStatus">The remote conversationStatus override to select next workflow.</param>
	/// <returns>True is next workflow selected successfully or current workflow will continue.</returns>
	bool SelectNextWorkflow(string? conversationStatus);
}
