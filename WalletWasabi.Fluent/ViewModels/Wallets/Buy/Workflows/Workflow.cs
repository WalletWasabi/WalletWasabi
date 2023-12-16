using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;
using CountryState = WalletWasabi.WebClients.ShopWare.Models.State;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class Workflow : ReactiveObject
{
	[AutoNotify] private IWorkflowStep? _currentStep;
	[AutoNotify] private Conversation _conversation;

	protected Workflow(Conversation conversation)
	{
		_conversation = conversation;
	}

	public abstract Task ExecuteAsync();

	public abstract IMessageEditor MessageEditor { get; }

	protected async Task ExecuteStepAsync(IWorkflowStep step)
	{
		CurrentStep = step;
		Conversation = await step.ExecuteAsync(Conversation);
	}

	public static Workflow Create(Wallet wallet, Conversation conversation)
	{
		// If another type of workflow is required in the future this is the place where it should be defined
		var workflow = new ShopinBitWorkflow(wallet, conversation);

		return workflow;
	}
}

//public class Conversation
//{
//	public ConversationId Id { get; set; } = ConversationId.Empty;
//	public List<Chat> ChatMessages { get; } = new();
//	public OrderStatus OrderStatus { get; set; }
//	public ConversationStatus ConversationStatus { get; set; }
//	public ConversationMetaData MetaData { get; } = new();
//}

//public class ConversationMetaData
//{
//	public string Title { get; set; }
//	public BuyAnythingClient.Product? Product { get; set; }
//	public Country? Country { get; set; }
//	public string? RequestedItem { get; set; }
//	public bool PrivacyPolicyAccepted { get; set; }
//	public string? FirstName { get; set; }
//	public string? LastName { get; set; }
//	public string? StreetName { get; set; }
//	public string? HouseNumber { get; set; }
//	public string? PostalCode { get; set; }
//	public string? City { get; set; }
//	public CountryState? State { get; set; }
//	public bool TermsAccepted { get; set; }
//	public bool OfferAccepted { get; set; }
//}

//public class ChatMessage
//{
//	public MessageSource Source { get; set; }
//	public string Text { get; set; }
//	public bool IsUnread { get; set; }
//	public string? StepName { get; set; }
//	public DataCarrier? Data { get; set; }

//	public bool IsMyMessage => Source == MessageSource.User;
//}
