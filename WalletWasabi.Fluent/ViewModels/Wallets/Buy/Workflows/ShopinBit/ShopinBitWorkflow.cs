using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class ShopinBitWorkflow : Workflow
{
	private readonly BuyAnythingManager _buyAnythingManager;
	private readonly Wallet _wallet;

	public ShopinBitWorkflow(Wallet wallet, Conversation conversation) : base(conversation)
	{
		_wallet = wallet;
		_buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();
	}

	public override async Task ExecuteAsync()
	{
		// Initial message + Select Product
		await ExecuteStepAsync(new WelcomeStep(Conversation));

		// Select Country
		await ExecuteStepAsync(new CountryStep(Conversation, _buyAnythingManager.Countries));

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

		// Firstname
		await ExecuteStepAsync(new FirstNameStep(Conversation));

		// Lastname
		await ExecuteStepAsync(new LastNameStep(Conversation));

		// Streetname
		await ExecuteStepAsync(new StreetNameStep(Conversation));

		// Housenumber
		await ExecuteStepAsync(new HouseNumberStep(Conversation));

		// ZIP/Postalcode
		await ExecuteStepAsync(new ZipPostalCodeStep(Conversation));

		// City
		await ExecuteStepAsync(new CityStep(Conversation));

		// State
		await ExecuteStepAsync(new StateStep(Conversation));

		// Accept Terms of service
		await ExecuteStepAsync(new ConfirmTosStep(Conversation));

		// Accept Offer
		await ExecuteStepAsync(new AcceptOfferStep(Conversation));

		// Save Conversation
		await ExecuteStepAsync(new SaveConversationStep(Conversation));

		// TODO: The wording is reviewed until this point.
	}

	public override IMessageEditor MessageEditor => new ShopinBitMessageEditor(this);

	/// <summary>
	/// Listen to Conversation Updates from the Server waiting for the specified Status. Upon that, it updates the Conversation, and optionally Ignores the current Chat Support Step.
	/// </summary>
	/// <returns>an IDisposable for the event subscription.</returns>
	/// <exception cref="InvalidOperationException"></exception>
	private IDisposable WaitForConversationStatus(ConversationStatus status, bool ignoreCurrentSupportStep)
	{
		return
			Observable.FromEventPattern<ConversationUpdateEvent>(_buyAnythingManager, nameof(BuyAnythingManager.ConversationUpdated))
					  .Where(x => x.EventArgs.Conversation.Id == Conversation.Id)
					  .Where(x => x.EventArgs.Conversation.ConversationStatus == status)
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Do(x =>
					  {
						  Conversation = x.EventArgs.Conversation;
						  if (ignoreCurrentSupportStep)
						  {
							  IgnoreCurrentSupportChatStep();
						  }
					  })
					  .Subscribe();
	}

	private void IgnoreCurrentSupportChatStep()
	{
		if (CurrentStep is SupportChatStep support)
		{
			support.Ignore();
		}
	}
}
