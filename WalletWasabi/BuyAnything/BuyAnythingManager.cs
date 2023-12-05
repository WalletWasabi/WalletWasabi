using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.BuyAnything;

// Event that is raised when we detect an update in the server
public record ConversationUpdateEvent(Conversation Conversation, DateTimeOffset LastUpdate);

public record ChatMessage(bool IsMyMessage, string Message);

public record Country(string Id, string Name);

[Flags]
internal enum ServerEvent
{
	None = 0, // do not remove this value
	MakeOffer = 1,
	ConfirmPayment = 2,
	InvalidateInvoice = 4,
	ReceiveInvoice = 8,
	FinishConversation = 16,
	ReceiveAttachments = 32,
	SendOrder = 64,
	ReceivePaymentAfterExpiration = 128,
	CloseOfferSuccessfully = 256
}

// Class to manage the conversation updates
public class BuyAnythingManager : PeriodicRunner
{
	public BuyAnythingManager(string dataDir, TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
		FilePath = Path.Combine(dataDir, "Conversations", "Conversations.json");
		ConversationUpdated += BuyAnythingManager_ConversationUpdated;
	}

	private void BuyAnythingManager_ConversationUpdated(object? sender, ConversationUpdateEvent e)
	{
		Logger.LogWarning($"ConvID: {e.Conversation.Id} OrderStatus: {e.Conversation.OrderStatus} ConvStatus: {e.Conversation.ConversationStatus} LastMessage: {e.Conversation.ChatMessages.Last().Message}");
	}

	private BuyAnythingClient Client { get; }
	private static List<Country> Countries { get; } = new();

	private ConversationTracking ConversationTracking { get; } = new();
	private bool IsConversationsLoaded { get; set; }

	private string FilePath { get; }

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Load the conversations from the disk in case they were not loaded yet
		await EnsureConversationsAreLoadedAsync(cancel).ConfigureAwait(false);

		// Iterate over the conversations that are updatable
		foreach (var track in ConversationTracking.UpdatableConversations)
		{
			// Check if there is new info in the chat
			await CheckUpdateInChatAsync(track, cancel).ConfigureAwait(false);

			// Check if the order state has changed and update the conversation status.
			await CheckUpdateInOrderStatusAsync(track, cancel).ConfigureAwait(false);
		}
	}

	private async Task CheckUpdateInOrderStatusAsync(ConversationUpdateTrack track, CancellationToken cancel)
	{
		var orders = await Client
			.GetOrdersUpdateAsync(track.Credential, cancel)
			.ConfigureAwait(false);

		// There is only one order per customer  and that's why we request all the orders
		// but with only expect to get one. However, it could happen that the order was
		// deleted and then we can have zero orders
		if (orders.FirstOrDefault() is not { } order)
		{
			return;
		}

		var orderCustomFields = order.CustomFields;

		var serverEvent = GetServerEvent(order, track.Conversation.OrderStatus);
		switch (track.Conversation.ConversationStatus)
		{
			// This means that in "lineItems" we have the offer data
			case ConversationStatus.Started
				when serverEvent.HasFlag(ServerEvent.MakeOffer):
				await SendSystemChatLinesAsync(track,
					ConvertOfferDetailToMessages(order),
					order.UpdatedAt, ConversationStatus.OfferReceived, cancel).ConfigureAwait(false);
				break;

			// Once the user accepts the offer, the system generates a bitcoin address and amount
			case ConversationStatus.OfferAccepted when serverEvent.HasFlag(ServerEvent.ReceiveInvoice):
				// case ConversationStatus.InvoiceInvalidated when serverEvent.HasFlag(ServerEvent.ReceiveNewInvoice):
				await SendSystemChatLinesAsync(track,
					$"Pay to: {orderCustomFields.Btcpay_PaymentLink}. The invoice expires in 10 minutes",
					order.UpdatedAt, ConversationStatus.InvoiceReceived,
					cancel).ConfigureAwait(false);
				break;

			// The status changes to "In Progress" after the user paid
			case ConversationStatus.InvoiceReceived
				or ConversationStatus.InvoicePaidAfterExpiration // if we paid a bit late but the order was sent, that means everything is alright
				when serverEvent.HasFlag(ServerEvent.ConfirmPayment):
				await SendSystemChatLinesAsync(track, "Payment confirmed",
					order.UpdatedAt, ConversationStatus.PaymentConfirmed, cancel).ConfigureAwait(false);
				break;

			// In case the invoice expires we communicate this fact to the chat
			case ConversationStatus.InvoiceReceived
				when serverEvent.HasFlag(ServerEvent.InvalidateInvoice):
				await SendSystemChatLinesAsync(track, "Invoice has expired",
					order.UpdatedAt, ConversationStatus.InvoiceExpired, cancel).ConfigureAwait(false);
				break;

			// In case the invoice expires we communicate this fact to the chat
			case ConversationStatus.InvoiceReceived
				when serverEvent.HasFlag(ServerEvent.ReceivePaymentAfterExpiration):
				await SendSystemChatLinesAsync(track, "Payment received after invoice expiration. In case this is a problem, an agent will get in contact with you.",
					order.UpdatedAt, ConversationStatus.InvoicePaidAfterExpiration, cancel).ConfigureAwait(false);
				break;

			// Payment is confirmed and status is SHIPPED the we have a tracking link to display
			case ConversationStatus.PaymentConfirmed
				when serverEvent.HasFlag(ServerEvent.SendOrder):
				{
					var trackingCodes = order.Deliveries.SelectMany(x => x.TrackingCodes).ToArray();

					if (trackingCodes.Any())
					{
						var newMessage = "Tracking link" + (trackingCodes.Length >= 2 ? "s" : "");
						await SendSystemChatLinesAsync(track,
							  $"{newMessage}:\n {string.Join("\n", trackingCodes)}",
						order.UpdatedAt, ConversationStatus.Shipped, cancel).ConfigureAwait(false);
					}

					track.Conversation = track.Conversation with
					{
						ConversationStatus = ConversationStatus.Shipped
					};
				}
				break;

			// In case the order was paid and/or shipped and the order is closed containing attachments we send them to the ui
			case ConversationStatus.Shipped or ConversationStatus.PaymentConfirmed
				when serverEvent.HasFlag(ServerEvent.CloseOfferSuccessfully):
				await SendSystemChatLinesAsync(track,$"Check the attached file \n {GetLinksByLine(orderCustomFields.Concierge_Request_Attachements_Links)}",
					order.UpdatedAt, ConversationStatus.Finished, cancel).ConfigureAwait(false);
				break;

			default:
				if (serverEvent.HasFlag(ServerEvent.FinishConversation))
				{
					await SendSystemChatLinesAsync(track, "Conversation Finished.", order.UpdatedAt, ConversationStatus.Finished, cancel).ConfigureAwait(false);
				}
				// TODO: Handle unexpected phase changes.
				break;
		}
	}

	private async Task CheckUpdateInChatAsync(ConversationUpdateTrack track, CancellationToken cancel)
	{
		// Get full customer profile to get updated messages.
		var customerProfileResponse = await Client
			.GetCustomerProfileAsync(track.Credential, track.LastUpdate, cancel)
			.ConfigureAwait(false);

		var customer = customerProfileResponse;
		var fullConversation = customerProfileResponse.CustomFields.Wallet_Chat_Store;
		var messages = Chat.FromText(fullConversation);
		if (messages.Count > track.Conversation.ChatMessages.Count)
		{
			track.Conversation = track.Conversation with
			{
				ChatMessages = messages,
			};
			ConversationUpdated.SafeInvoke(this,
				new ConversationUpdateEvent(track.Conversation, customer.UpdatedAt ?? DateTimeOffset.Now));

			await SaveAsync(cancel).ConfigureAwait(false);
		}
	}

	public int GetNextConversationId(string walletId) =>
		ConversationTracking.GetNextConversationId(walletId);

	public async Task<Conversation[]> GetConversationsAsync(string walletId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return ConversationTracking.GetConversationsByWalletId(walletId);
	}

	public async Task<Conversation> GetConversationByIdAsync(ConversationId conversationId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return ConversationTracking.GetConversationsById(conversationId);
	}

	public async Task<int> RemoveConversationsByIdsAsync(IEnumerable<ConversationId> toRemoveIds, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		var removedCount = ConversationTracking.RemoveAll(x => toRemoveIds.Contains(x.Conversation.Id));
		if (removedCount > 0)
		{
			await SaveAsync(cancellationToken).ConfigureAwait(false);
		}

		return removedCount;
	}

	public async Task<State[]> GetStatesForCountryAsync(string countryName, CancellationToken cancellationToken)
	{
		var country = Countries.FirstOrDefault(c => c.Name == countryName) ?? throw new InvalidOperationException($"Country {countryName} doesn't exist.");
		return await Client.GetStatesbyCountryIdAsync(country.Id, cancellationToken).ConfigureAwait(false);
	}

	public async Task<Country[]> GetCountriesAsync(CancellationToken cancellationToken)
	{
		await EnsureCountriesAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return Countries.ToArray();
	}

	public async Task StartNewConversationAsync(string walletId, string countryId, BuyAnythingClient.Product product, string message, ChatMessage[] chatMessages, string title, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);

		var credential = GenerateRandomCredential();
		var orderId = await Client.CreateNewConversationAsync(credential.UserName, credential.Password, countryId, product, message, cancellationToken)
			.ConfigureAwait(false);
		var conversation = new Conversation(
				new ConversationId(walletId, credential.UserName, credential.Password, orderId),
				new Chat(chatMessages),
				OrderStatus.Open,
				ConversationStatus.Started,
				title);
		ConversationTracking.Add(new ConversationUpdateTrack(conversation));

		ConversationUpdated?.SafeInvoke(this, new(conversation, DateTimeOffset.Now));
		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task UpdateConversationAsync(ConversationId conversationId, IEnumerable<ChatMessage> chatMessages, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		var track = ConversationTracking.GetConversationTrackByd(conversationId);
		track.Conversation = track.Conversation with
		{
			ChatMessages = new(chatMessages),
		};
		track.LastUpdate = DateTimeOffset.Now;

		var rawText = track.Conversation.ChatMessages.ToText();
		await Client.UpdateConversationAsync(track.Credential, rawText, cancellationToken).ConfigureAwait(false);

		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task AcceptOfferAsync(ConversationId conversationId, string firstName, string lastName, string address, string houseNumber, string zipCode, string city, string stateId, string countryId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);

		var track = ConversationTracking.GetConversationTrackByd(conversationId);
		if (track.Conversation.ConversationStatus == ConversationStatus.InvoiceExpired)
		{
			throw new InvalidOperationException("Invoice has expired.");
		}
		await Client.SetBillingAddressAsync(track.Credential, firstName, lastName, address, houseNumber, zipCode, city, stateId, countryId, cancellationToken).ConfigureAwait(false);
		await Client.HandlePaymentAsync(track.Credential, track.Conversation.Id.OrderId, cancellationToken).ConfigureAwait(false);
		track.Conversation = track.Conversation with
		{
			ChatMessages = track.Conversation.ChatMessages.AddSentMessage("Offer accepted"),
			ConversationStatus = ConversationStatus.OfferAccepted
		};
		track.LastUpdate = DateTimeOffset.Now;

		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	private static OrderStatus GetOrderStatus(Order order)
	{
		var orderStatus = order.StateMachineState.Name switch
		{
			"Open" => OrderStatus.Open,
			"In Progress" => OrderStatus.InProgress,
			"Cancelled" => OrderStatus.Cancelled,
			"Done" => OrderStatus.Done,
			_ => throw new ArgumentException($"Unexpected {order.StateMachineState.Name} status.")
		};
		return orderStatus;
	}

	private ServerEvent GetServerEvent(Order order, OrderStatus currentOrderStatus)
	{
		ServerEvent events = 0;
		if (order.CustomFields?.Concierge_Request_Status_State == "OFFER")
		{
			events |= ServerEvent.MakeOffer;
		}

		var orderStatus = GetOrderStatus(order);
		if (orderStatus != currentOrderStatus) // the order status changed
		{
			if (orderStatus == OrderStatus.InProgress && currentOrderStatus == OrderStatus.Open)
			{
				events |= ServerEvent.ConfirmPayment;
			}

			if (orderStatus == OrderStatus.Done)
			{
				if (!string.IsNullOrWhiteSpace(order.CustomFields?.Concierge_Request_Attachements_Links))
				{
					events |= ServerEvent.CloseOfferSuccessfully;
				}
				else
				{
					events |= ServerEvent.FinishConversation;
				}
			}
		}

		if (order.CustomFields?.BtcpayOrderStatus == "invoiceExpired")
		{
			if (order.CustomFields.PaidAfterExpiration)
			{
				events |= ServerEvent.ReceivePaymentAfterExpiration;
			}
			else
			{
				events |= ServerEvent.InvalidateInvoice;
			}
		}

		if (!string.IsNullOrWhiteSpace(order.CustomFields?.Btcpay_PaymentLink))
		{
			events |= ServerEvent.ReceiveInvoice;
		}

		if (!string.IsNullOrWhiteSpace(order.CustomFields?.Concierge_Request_Attachements_Links))
		{
			events |= ServerEvent.ReceiveAttachments;
		}

		if (order.Deliveries.Any(d => d.StateMachineState.Name == "Shipped"))
		{
			events |= ServerEvent.SendOrder;
		}
		return events;
	}

	private async Task SendSystemChatLinesAsync(ConversationUpdateTrack track, string message, DateTimeOffset? updatedAt, ConversationStatus newStatus, CancellationToken cancellationToken)
	{
		var updatedConversation = track.Conversation.AddSystemChatLine(message, newStatus);
		ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(updatedConversation, updatedAt ?? DateTimeOffset.Now));
		await SaveAsync(cancellationToken).ConfigureAwait(false);
		track.Conversation = updatedConversation;
	}

	private async Task FinishConversationAsync(ConversationUpdateTrack track, CancellationToken cancellationToken)
	{
		var updatedConversation = track.Conversation.AddSystemChatLine("Conversation finished.", ConversationStatus.Finished);
		track.Conversation = updatedConversation;
		ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(updatedConversation, DateTimeOffset.Now));
		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(ConversationTracking, Formatting.Indented);
		await File.WriteAllTextAsync(FilePath, json, cancellationToken).ConfigureAwait(false);
	}

	public static string GetWalletId(Wallet wallet) =>
		wallet.KeyManager.MasterFingerprint is { } masterFingerprint
			? masterFingerprint.ToString()
			: "readonly wallet";

	private string GetLinksByLine(string attachmentsLinks) =>
		string.Join("\n", attachmentsLinks
			.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

	private static string ConvertOfferDetailToMessages(Order order)
	{
		StringBuilder sb = new();
		foreach (var lineItem in order.LineItems)
		{
			sb.AppendLine($"{lineItem.Quantity} x {lineItem.Label} ---unit price: {lineItem.UnitPrice} ---total price: {lineItem.TotalPrice}");
		}

		return sb.ToString();
	}

	private async Task EnsureConversationsAreLoadedAsync(CancellationToken cancellationToken)
	{
		if (!IsConversationsLoaded)
		{
			await LoadConversationsAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task LoadConversationsAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureFileExists(FilePath);
		string json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
		var conversations = JsonConvert.DeserializeObject<ConversationTracking>(json) ?? new();
		ConversationTracking.Load(conversations);
		IsConversationsLoaded = true;
	}

	private async Task EnsureCountriesAreLoadedAsync(CancellationToken cancellationToken)
	{
		if (Countries.Count == 0)
		{
			await LoadCountriesAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task LoadCountriesAsync(CancellationToken cancellationToken)
	{
		var assembly = System.Reflection.Assembly.GetAssembly(typeof(BuyAnythingManager));
		var assemblyDir = Path.GetDirectoryName(assembly.Location);
		var countriesFilePath = Path.Combine(assemblyDir, "BuyAnything/Data/Countries.json");
		var fileContent = await File.ReadAllTextAsync(countriesFilePath, cancellationToken).ConfigureAwait(false);

		Country[] countries = JsonConvert.DeserializeObject<Country[]>(fileContent)
						?? throw new InvalidOperationException("Couldn't read cached countries values.");

		Countries.AddRange(countries);
	}

	private NetworkCredential GenerateRandomCredential() =>
		new(
			userName: $"{Guid.NewGuid()}@me.com",
			password: RandomString.AlphaNumeric(25));

	public override void Dispose()
	{
		ConversationUpdated -= BuyAnythingManager_ConversationUpdated;
		base.Dispose();
	}
}
