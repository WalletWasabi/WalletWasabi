using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract class TextInputStep : WorkflowStep2<string>
{
	protected TextInputStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override string? StringValue(string value) => value;

	protected override bool ValidateInitialValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());

	protected override bool ValidateUserValue(string? value) => !string.IsNullOrWhiteSpace(value?.Trim());
}

public class FirstNameStep : TextInputStep
{
	public FirstNameStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:";

		yield return "Your First Name:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { FirstName = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.FirstName;
}

public class LastNameStep : TextInputStep
{
	public LastNameStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "Your Last Name:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) => conversation.UpdateMetadata(m => m with { LastName = value });

	protected override string? RetrieveValue(Conversation2 conversation) => conversation.MetaData.LastName;
}

public class StreetNameStep : TextInputStep
{
	public StreetNameStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "Street Name:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { StreetName = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.StreetName;
}

public class HouseNumberStep : TextInputStep
{
	public HouseNumberStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "House Number:"
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { HouseNumber = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.HouseNumber;
}

public class ZipPostalCodeStep : TextInputStep
{
	public ZipPostalCodeStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "ZIP/Postal Code:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { PostalCode = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.PostalCode;
}

public class CityStep : TextInputStep
{
	public CityStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "City";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { City = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.City;
}

// State
new (false,
				new DefaultInputValidator(
					workflowState,
					() => "State:"),
				CanSkipStateStep(states)),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new StateInputValidator(
					workflowState,
					states,
					ChatMessageMetaData.ChatMessageTag.State),
				null,
				CanSkipStateStep(states)),
			// Accept Terms of service
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "Thank you for providing your details. Please double-check them for accuracy. If everything looks good, agree to our Terms and Conditions and click 'BUY NOW' to proceed")),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmTosInputValidator(
					workflowState,
					new LinkViewModel
					{
						Link = termsOfServiceUrl,
						Description = "Accept the Terms of service",
						IsClickable = true
					},
					() => null,
					"BUY NOW")),
