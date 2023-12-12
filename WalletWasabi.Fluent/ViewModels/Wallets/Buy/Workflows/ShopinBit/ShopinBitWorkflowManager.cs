using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using WalletWasabi.BuyAnything;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManager : WorkflowManager
{
	private readonly string _walletId;
	private readonly Country[] _countries;
	private readonly BehaviorSubject<bool> _idChangedSubject;
	private BuyAnythingClient.Product? _product;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private ConversationId _id = ConversationId.Empty;

	public ShopinBitWorkflowManager(string walletId, Country[] countries)
	{
		_walletId = walletId;
		_countries = countries;
		_idChangedSubject = new BehaviorSubject<bool>(false);
		IdChangedObservable = _idChangedSubject.AsObservable();
	}

	public string WalletId => _walletId;

	public IObservable<bool> IdChangedObservable { get; }

	public void UpdateConversationId(ConversationId newId)
	{
		if (Id != ConversationId.Empty)
		{
			throw new InvalidOperationException("ID cannot be modified!");
		}

		Id = newId;
		_idChangedSubject.OnNext(true);
	}

	/// <summary>
	/// Selects next scripted workflow or use conversationStatus to override.
	/// </summary>
	/// <param name="conversationStatus">The remote conversationStatus override to select next workflow.</param>
	/// <param name="args"></param>
	/// <returns>True is next workflow selected successfully or current workflow will continue.</returns>
	public bool TryToSetNextWorkflow(string? conversationStatus, object? args)
	{
		var states = args as WebClients.ShopWare.Models.State[];

		if (CurrentWorkflow is InitialWorkflow initialWorkflow)
		{
			_product = initialWorkflow.Product;
		}

		if (CurrentWorkflow is PrivacyPolicyWorkflow)
		{
			_product = null;
		}

		if (conversationStatus is not null)
		{
			if (CurrentWorkflow?.CanCancel() ?? true)
			{
				CurrentWorkflow = GetShopinBitWorkflowFromConversation(conversationStatus, states, _product);
				return true;
			}

			return false;
		}

		CurrentWorkflow = CurrentWorkflow switch
		{
			null => new InitialWorkflow(WorkflowState, _countries),
			InitialWorkflow => new PrivacyPolicyWorkflow(WorkflowState, _product),
			PrivacyPolicyWorkflow => new SupportChatWorkflow(WorkflowState),
			DeliveryWorkflow => new SupportChatWorkflow(WorkflowState),
			SupportChatWorkflow => new SupportChatWorkflow(WorkflowState),
			_ => CurrentWorkflow
		};

		return true;
	}

	public override bool OnInvokeNextWorkflow(
		string? context,
		object? args,
		Action<string, ChatMessageMetaData> onAssistantMessage,
		CancellationToken cancellationToken)
	{
		var states = args as WebClients.ShopWare.Models.State[];

		TryToSetNextWorkflow(context, states);

		WorkflowState.SignalValid(false);
		InvokeOutputWorkflows(onAssistantMessage, cancellationToken);

		// Continue the loop until next workflow is there and is completed.
		if (CurrentWorkflow is null)
		{
			return true;
		}

		if (CurrentWorkflow.IsCompleted)
		{
			TryToSetNextWorkflow(null, cancellationToken);
		}

		return true;
	}

	private Workflow? GetShopinBitWorkflowFromConversation(
		string? conversationStatus,
		WebClients.ShopWare.Models.State[] states,
		BuyAnythingClient.Product? product)
	{
		return conversationStatus switch
		{
			"Started" => new InitialWorkflow(WorkflowState, _countries),
			"OfferReceived" => product is not null
				? new PrivacyPolicyWorkflow(WorkflowState, product)
				: new DeliveryWorkflow(WorkflowState, states),
			"PaymentDone" => new SupportChatWorkflow(WorkflowState),
			"PaymentConfirmed" => new SupportChatWorkflow(WorkflowState),
			"OfferAccepted" => new SupportChatWorkflow(WorkflowState),
			"InvoiceReceived" => new SupportChatWorkflow(WorkflowState),
			"InvoiceExpired" => new SupportChatWorkflow(WorkflowState),
			"InvoicePaidAfterExpiration" => new SupportChatWorkflow(WorkflowState),
			"Shipped" => new SupportChatWorkflow(WorkflowState),
			"Finished" => new SupportChatWorkflow(WorkflowState),
			"Support" => new SupportChatWorkflow(WorkflowState),
			_ => null
		};
	}
}
