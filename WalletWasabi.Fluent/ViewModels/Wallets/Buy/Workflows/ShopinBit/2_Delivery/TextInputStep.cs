using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract class TextInputStep : WorkflowStep<string>
{
	protected TextInputStep(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
	}

	protected override string? StringValue(string value) => value;

	protected override bool ValidateInitialValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	public override async Task ExecuteAsync()
	{
		IsBusy = true;
		await base.ExecuteAsync();
		IsBusy = false;
	}
}
