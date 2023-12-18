using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit._4___Finished;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class ShopinBitWorkflow : Workflow
{
	private readonly BuyAnythingManager _buyAnythingManager;
	private readonly Wallet _wallet;

	private CancellationToken? _token;

	public ShopinBitWorkflow(Wallet wallet, Conversation conversation) : base(conversation)
	{
		_wallet = wallet;
		_buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();
	}

	public override async Task ExecuteAsync(CancellationToken token)
	{
		_token = token;

		// Initial message + Select Product
		await ExecuteStepAsync(new WelcomeStep(Conversation, token));

		// Select Country
		await ExecuteStepAsync(new CountryStep(Conversation, _buyAnythingManager.Countries, token));

		// Specify your request
		await ExecuteStepAsync(new RequestedItemStep(Conversation, token));

		// Accept Privacy Policy
		await ExecuteStepAsync(new PrivacyPolicyStep(Conversation, token));

		// Start Conversation (only if it's a new Conversation)
		await ExecuteStepAsync(new StartConversationStep(Conversation, _wallet, token));

		// Save the entire conversation
		await ExecuteStepAsync(new SaveConversationStep(Conversation, token));

		using (ListenToServerUpdates())
		{
			// Wait until the Offer is received
			while (!Conversation.MetaData.OfferReceived)
			{
				// User might send chat messages to Support Agent
				await ExecuteStepAsync(new SupportChatStep(Conversation, token));
			}
		}

		// Firstname
		await ExecuteStepAsync(new FirstNameStep(Conversation, token));

		// Lastname
		await ExecuteStepAsync(new LastNameStep(Conversation, token));

		// Streetname
		await ExecuteStepAsync(new StreetNameStep(Conversation, token));

		// Housenumber
		await ExecuteStepAsync(new HouseNumberStep(Conversation, token));

		// ZIP/Postalcode
		await ExecuteStepAsync(new ZipPostalCodeStep(Conversation, token));

		// City
		await ExecuteStepAsync(new CityStep(Conversation, token));

		// State
		await ExecuteStepAsync(new StateStep(Conversation, token));

		// Accept Terms of service
		await ExecuteStepAsync(new ConfirmTosStep(Conversation, token));

		// Accept Offer
		await ExecuteStepAsync(new AcceptOfferStep(Conversation, token));

		// Save Conversation
		await ExecuteStepAsync(new SaveConversationStep(Conversation, token));

		using (ListenToServerUpdates())
		{
			// Wait until the Conversation is deleted on SIB side
			while (Conversation.ConversationStatus != ConversationStatus.Deleted)
			{
				if (Conversation.ConversationStatus == ConversationStatus.Finished)
				{
					IsCompleted = true;
					await ExecuteStepAsync(new OrderFinishedMessage(Conversation, token));
				}

				// User might send chat messages to Support Agent
				await ExecuteStepAsync(new SupportChatStep(Conversation, token));
			}
		}

		WorkflowCompleted();
	}

	public override IMessageEditor MessageEditor => new ShopinBitMessageEditor(this, _token);

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
