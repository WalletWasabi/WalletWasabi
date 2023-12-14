using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class SupportChatStep : WorkflowStep2<string>
{
	public SupportChatStep(Conversation2 conversation) : base(conversation)
	{
	}

	public override Task<Conversation2> ExecuteAsync(Conversation2 conversation)
	{
		return base.ExecuteAsync(conversation);
	}

	/// <summary>
	/// Ignores the Support Chat message entered by the user. Used when another step is executed due to external reasons (such as Offer Received)
	/// </summary>
	public void Ignore() => SetCompleted();

	protected override Conversation2 PutValue(Conversation2 conversation, string value) => conversation;

	protected override string? RetrieveValue(Conversation2 conversation) => null;

	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());
}
