using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public class ShopinBitChatMessageEditor : IChatMessageEditor
{
	private readonly ShopinBitWorkflow _workflow;

	public ShopinBitChatMessageEditor(ShopinBitWorkflow workflow)
	{
		_workflow = workflow;
	}

	private Conversation Conversation => _workflow.Conversation;

	public bool IsEditable(ChatMessage chatMessage)
	{
		return
			Conversation.ConversationStatus switch
			{
				ConversationStatus.OfferReceived =>
					chatMessage.StepName is nameof(FirstNameStep)
										 or nameof(LastNameStep)
										 or nameof(StreetNameStep)
										 or nameof(HouseNumberStep)
										 or nameof(ZipPostalCodeStep)
										 or nameof(CityStep)
										 or nameof(StateStep),
				_ => false
			};
	}

	public IWorkflowStep? GetEditor(ChatMessage chatMessage)
	{
		return chatMessage.StepName switch
		{
			// I could have used reflection (or a Source Generator LOL)
			nameof(FirstNameStep) => new FirstNameStep(Conversation),
			nameof(LastNameStep) => new LastNameStep(Conversation),
			nameof(StreetNameStep) => new StreetNameStep(Conversation),
			nameof(HouseNumberStep) => new HouseNumberStep(Conversation),
			nameof(ZipPostalCodeStep) => new ZipPostalCodeStep(Conversation),
			nameof(CityStep) => new CityStep(Conversation),
			nameof(StateStep) => new StateStep(Conversation),
			_ => null
		};
	}
}
