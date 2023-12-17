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

		// Save the entire conversation
		await ExecuteStepAsync(new SaveConversationStep(Conversation));

		using (ListenToServerUpdates())
		{
			// Wait until the Offer is received
			while (Conversation.ConversationStatus != ConversationStatus.OfferReceived)
			{
				// User might send chat messages to Support Agent
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

		using (ListenToServerUpdates())
		{
			// Wait until the Conversation is finished
			while (Conversation.ConversationStatus != ConversationStatus.Finished)
			{
				// User might send chat messages to Support Agent
				await ExecuteStepAsync(new SupportChatStep(Conversation));
			}
		}

		CurrentStep = null;
	}

	public override IMessageEditor MessageEditor => new ShopinBitMessageEditor(this);

	/// <summary>
	/// Listen to Conversation Updates from the Server. Upon that, it updates the Conversation
	/// </summary>
	/// <returns>an IDisposable for the event subscription.</returns>
	/// <remarks>if the ConversationStatus changes, this handler will Ignore the Current Step</remarks>
	/// <exception cref="InvalidOperationException"></exception>
	private IDisposable ListenToServerUpdates()
	{
		return
			Observable.FromEventPattern<ConversationUpdateEvent>(_buyAnythingManager, nameof(BuyAnythingManager.ConversationUpdated))
					  .Where(x => x.EventArgs.Conversation.Id == Conversation.Id)
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Do(x =>
					  {
						  var oldStatus = Conversation.ConversationStatus;
						  var newStatus = x.EventArgs.Conversation.ConversationStatus;

						  Conversation = x.EventArgs.Conversation;

						  if (oldStatus != newStatus)
						  {
							  CurrentStep?.Ignore();
						  }
					  })
					  .Subscribe();
	}
}
