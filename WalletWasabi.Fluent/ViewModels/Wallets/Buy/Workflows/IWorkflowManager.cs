using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowManager
{
	public IObservable<bool> IdChangedObservable { get; }

	IWorkflowValidator WorkflowValidator { get; }

	public ConversationId Id { get; }

	Workflow? CurrentWorkflow { get; }

	Task SendChatHistoryAsync(ChatMessage[] chatMessages, CancellationToken cancellationToken);

	Task UpdateConversationLocallyAsync(ChatMessage[] chatMessages, CancellationToken cancellationToken);

	Task SendApiRequestAsync(ChatMessage[] chatMessages, ConversationMetaData metaData, CancellationToken cancellationToken);

	bool SelectNextWorkflow(string? conversationStatus, object? args);

	void UpdateId(ConversationId id);

	void ResetWorkflow();

	void Update(Action<string> onNewMessage);
}
