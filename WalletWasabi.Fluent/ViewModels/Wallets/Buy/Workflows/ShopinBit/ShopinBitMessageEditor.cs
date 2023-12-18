using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public class ShopinBitMessageEditor : IMessageEditor
{
	private readonly ShopinBitWorkflow _workflow;
	private readonly CancellationToken? _token;

	public ShopinBitMessageEditor(ShopinBitWorkflow workflow, CancellationToken? token)
	{
		_workflow = workflow;
		_token = token;
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

	public IWorkflowStep? Get(ChatMessage chatMessage)
	{
		if (_token is not { } token)
		{
			return null;
		}

		return chatMessage.StepName switch
		{
			// I could have used reflection (or a Source Generator LOL)
			nameof(FirstNameStep) => new FirstNameStep(Conversation, token, true),
			nameof(LastNameStep) => new LastNameStep(Conversation, token, true),
			nameof(StreetNameStep) => new StreetNameStep(Conversation, token, true),
			nameof(HouseNumberStep) => new HouseNumberStep(Conversation, token, true),
			nameof(ZipPostalCodeStep) => new ZipPostalCodeStep(Conversation, token, true),
			nameof(CityStep) => new CityStep(Conversation, token, true),
			nameof(StateStep) => new StateStep(Conversation, token, true),
			_ => null
		};
	}
}
