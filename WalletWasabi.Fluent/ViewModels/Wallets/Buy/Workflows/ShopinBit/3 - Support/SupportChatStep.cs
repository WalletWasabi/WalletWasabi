using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class SupportChatStep : WorkflowStep<string>
{
	public SupportChatStep(Conversation conversation) : base(conversation)
	{
	}

	public override async Task ExecuteAsync()
	{
		await base.ExecuteAsync();

		IsBusy = true;

		await new SaveConversationStep(Conversation).ExecuteAsync();

		IsBusy = false;
	}

	protected override Conversation PutValue(Conversation conversation, string value) => conversation;

	protected override string? RetrieveValue(Conversation conversation) => null;

	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override string? StringValue(string value) => value.Trim();
}
