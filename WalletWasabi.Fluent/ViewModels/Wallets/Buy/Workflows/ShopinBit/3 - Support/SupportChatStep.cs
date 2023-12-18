using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class SupportChatStep : WorkflowStep<string>
{
	private readonly CancellationToken _token;
	private bool crash = true;

	public SupportChatStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
		_token = token;
	}

	public override async Task ExecuteAsync()
	{
		await base.ExecuteAsync();

		IsBusy = true;

		try
		{
			if (crash)
			{
				crash = false;
				throw new InvalidOperationException($"Prueba");
			}

			await new SaveConversationStep(Conversation, _token).ExecuteAsync();
		}
		finally
		{
			IsBusy = false;
		}
	}

	protected override Conversation PutValue(Conversation conversation, string value) => conversation;

	protected override string? RetrieveValue(Conversation conversation) => null;

	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override string? StringValue(string value) => value.Trim();
}
