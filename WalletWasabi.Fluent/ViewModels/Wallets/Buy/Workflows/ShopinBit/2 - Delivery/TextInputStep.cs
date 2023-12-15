using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract class TextInputStep : WorkflowStep<string>
{
	protected TextInputStep(Conversation conversation) : base(conversation)
	{
	}

	protected override string? StringValue(string value) => value;

	protected override bool ValidateInitialValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());
}
