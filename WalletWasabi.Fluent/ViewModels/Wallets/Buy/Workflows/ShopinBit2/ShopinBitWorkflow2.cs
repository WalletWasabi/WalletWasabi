using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class ShopinBitWorkflow2 : Workflow2
{
	private readonly Wallet _wallet;

	public ShopinBitWorkflow2(Wallet wallet, Conversation2 conversation) : base(conversation)
	{
		_wallet = wallet;
	}

	public override async Task<Conversation2> ExecuteAsync()
	{
		// Initial message + Select Product
		await ExecuteStepAsync(new WelcomeStep(Conversation));

		// Select Country
		await ExecuteStepAsync(new CountryStep(Conversation));

		// Specify your request
		await ExecuteStepAsync(new RequestedItemStep(Conversation));

		// Accept Privacy Policy
		await ExecuteStepAsync(new PrivacyPolicyStep(Conversation));

		// Start Conversation (only if it's a new Conversation)
		await ExecuteStepAsync(new StartConversationStep(Conversation, _wallet));

		// Wait until the Offer is received
		using (WaitForConversationStatus(ConversationStatus.OfferReceived, true))
		{
			// Support Chat (loops until Conversation Updates)
			while (Conversation.ConversationStatus != ConversationStatus.OfferReceived)
			{
				await ExecuteStepAsync(new SupportChatStep(Conversation));
			}
		}

		return Conversation;
	}

	private void IgnoreCurrentSupportChatStep()
	{
		if (CurrentStep is SupportChatStep support)
		{
			support.Ignore();
		}
	}

	/// <summary>
	/// Listen to Conversation Updates from the Server waiting for the specified Status. Upon that, it updates the Conversation, and optionally Ignores the current Chat Support Step.
	/// </summary>
	/// <returns>an IDisposable for the event subscription.</returns>
	/// <exception cref="InvalidOperationException"></exception>
	private IDisposable WaitForConversationStatus(ConversationStatus status, bool ignoreCurrentSupportStep)
	{
		if (BuyAnythingManager is not { })
		{
			throw new InvalidOperationException($"BuyAnythingManager not initialized.");
		}

		return
			Observable.FromEventPattern<ConversationUpdateEvent2>(BuyAnythingManager, nameof(BuyAnythingManager.ConversationUpdated))
						  .Where(x => x.EventArgs.Conversation.Id == Conversation.Id)
						  .Where(x => x.EventArgs.Conversation.ConversationStatus == status)
						  .Do(x =>
						  {
							  SetConversation(x.EventArgs.Conversation);
							  if (ignoreCurrentSupportStep)
							  {
								  IgnoreCurrentSupportChatStep();
							  }
						  })
						  .Subscribe();
	}

	private BuyAnythingManager2? BuyAnythingManager => Services.HostedServices.GetOrDefault<BuyAnythingManager2>();
}
