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

public record ChatMessage(bool IsMyMessage, string Message, bool IsUnread, ChatMessageMetaData MetaData);

public record Country(string Id, string Name);

public record ChatMessageMetaData(ChatMessageMetaData.ChatMessageTag Tag)
{
	public static readonly ChatMessageMetaData Empty = new(ChatMessageTag.None);

	public enum ChatMessageTag
	{
		None = 0,

		AssistantType = 11,
		Country = 12,

		FirstName = 21,
		LastName = 22,
		StreetName = 23,
		HouseNumber = 24,
		PostalCode = 25,
		City = 26,
		State = 27,

		PaymentInfo = 31,
	}
}

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
	CloseOfferSuccessfully = 256,
	GenerateNewInvoice = 512
}

// Class to manage the conversation updates
public class BuyAnythingManager : PeriodicRunner
{
	public BuyAnythingManager(string dataDir, TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
		FilePath = Path.Combine(dataDir, "Conversations", "Conversations.json");

		string countriesFilePath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "BuyAnything", "Data", "Countries.json");
		string fileContent = File.ReadAllText(countriesFilePath);
		Country[] countries = JsonConvert.DeserializeObject<Country[]>(fileContent)
			?? throw new InvalidOperationException("Couldn't read the countries list.");

		Countries = new List<Country>(countries);

		ConversationUpdated += BuyAnythingManager_ConversationUpdated;
	}

	private void BuyAnythingManager_ConversationUpdated(object? sender, ConversationUpdateEvent e)
	{
		Logger.LogWarning($"ConvID: {e.Conversation.Id} OrderStatus: {e.Conversation.OrderStatus} ConvStatus: {e.Conversation.ConversationStatus} LastMessage: {e.Conversation.ChatMessages.Last().Message}");
	}

	private BuyAnythingClient Client { get; }
	public IReadOnlyList<Country> Countries { get; }

	private ConversationTracking ConversationTracking { get; } = new();
	private bool IsConversationsLoaded { get; set; }
	private string FilePath { get; }

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Load the conversations from the disk in case they were not loaded yet
		await EnsureConversationsAreLoadedAsync(cancel).ConfigureAwait(false);

		// Iterate over the conversations that are updatable
		foreach (var track in ConversationTracking.GetUpdatableConversations())
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
			case ConversationStatus.OfferAccepted
				when serverEvent.HasFlag(ServerEvent.ReceiveInvoice):
				// case ConversationStatus.InvoiceInvalidated when serverEvent.HasFlag(ServerEvent.ReceiveNewInvoice):

				track.Invoice = new(orderCustomFields.Btcpay_PaymentLink);

				// Remove sending this chat once the UI can handle the track.Invoice and save the track.
				await SendSystemChatLinesAsync(track,
					$"Pay to: {orderCustomFields.Btcpay_PaymentLink}. The invoice expires in 10 minutes",
					order.UpdatedAt, ConversationStatus.InvoiceReceived,
					cancel).ConfigureAwait(false);
				break;

			case ConversationStatus.WaitingForInvoice
				when serverEvent.HasFlag(ServerEvent.MakeOffer):
				await HandlePaymentAsync(track, track.Conversation, cancel).ConfigureAwait(false);
				break;

			// The status changes to "In Progress" after the user paid
			case ConversationStatus.InvoiceReceived
				or ConversationStatus.InvoicePaidAfterExpiration // if we paid a bit late but the order was sent, that means everything is alright
				when serverEvent.HasFlag(ServerEvent.ConfirmPayment):
				await SendSystemChatLinesAsync(track, "Your payment is confirmed. Thank you for ordering with us. We will keep you updated here on the progress of your order.",
					order.UpdatedAt, ConversationStatus.PaymentConfirmed, cancel).ConfigureAwait(false);
				break;

			// In case the invoice expires we communicate this fact to the chat
			case ConversationStatus.InvoiceReceived
				when serverEvent.HasFlag(ServerEvent.InvalidateInvoice):
				await SendSystemChatLinesAsync(track, "Invoice Expired. Please send us your Bitcoin Transaction ID if you already have sent coins.",
					order.UpdatedAt, ConversationStatus.InvoiceExpired, cancel).ConfigureAwait(false);
				break;

			// In case the invoice expires we communicate this fact to the chat
			case ConversationStatus.InvoiceReceived
				when serverEvent.HasFlag(ServerEvent.ReceivePaymentAfterExpiration):
				await SendSystemChatLinesAsync(track, "Payment received after invoice expiration. In case this is a problem, an agent will get in contact with you.",
					order.UpdatedAt, ConversationStatus.InvoicePaidAfterExpiration, cancel).ConfigureAwait(false);
				break;

			// In case the invoice expired and a new one can be requested
			case ConversationStatus.InvoiceExpired
				when serverEvent.HasFlag(ServerEvent.GenerateNewInvoice):
				await SendSystemChatLinesAsync(track, "Our Team is reactivating the payment process.",
					order.UpdatedAt, ConversationStatus.WaitingForInvoice, cancel).ConfigureAwait(false);
				break;

			// Payment is confirmed and status is SHIPPED the we have a tracking link to display
			case ConversationStatus.PaymentConfirmed
				when serverEvent.HasFlag(ServerEvent.SendOrder):
				{
					var trackingCodes = order.Deliveries.SelectMany(x => x.TrackingCodes).ToArray();

					if (trackingCodes.Any())
					{
						var plural = trackingCodes.Length >= 2 ? "s" : "";
						await SendSystemChatLinesAsync(track, $"Tracking link{plural}:",
							order.UpdatedAt, ConversationStatus.Shipped, cancel).ConfigureAwait(false);

						foreach (var code in trackingCodes)
						{
							await SendSystemChatLinesAsync(track, code,
							order.UpdatedAt, ConversationStatus.Shipped, cancel).ConfigureAwait(false);
						}
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
				{
					var links = GetLinksByLine(orderCustomFields.Concierge_Request_Attachements_Links);
					if (links.Any())
					{
						var plural = links.Length >= 2 ? "s" : "";
						await SendSystemChatLinesAsync(track, $"Check the attached file{plural}:",
							order.UpdatedAt, ConversationStatus.Finished, cancel).ConfigureAwait(false);

						foreach (var link in links)
						{
							await SendSystemChatLinesAsync(track, link,
							order.UpdatedAt, ConversationStatus.Finished, cancel).ConfigureAwait(false);
						}
					}

					track.Conversation = track.Conversation with
					{
						ConversationStatus = ConversationStatus.Finished
					};
				}
				break;

			//Handle unexpected finished of the conversation
			case not ConversationStatus.Finished
				when serverEvent.HasFlag(ServerEvent.FinishConversation):
				await SendSystemChatLinesAsync(track, "Conversation Finished.", order.UpdatedAt, ConversationStatus.Finished, cancel).ConfigureAwait(false);
				break;

			default:
				// TODO: Handle unexpected phase changes?
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
		var messages = Chat.FromText(fullConversation, oldConversation: track.Conversation.ChatMessages);
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

	public async Task StartNewConversationAsync(string walletId, string countryName, BuyAnythingClient.Product product, ChatMessage[] chatMessages, ConversationMetaData metaData, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		string countryId = Countries.Single(c => c.Name == countryName).Id;
		var fullChat = new Chat(chatMessages);
		var credential = GenerateRandomCredential();
		var orderId = await Client.CreateNewConversationAsync(credential.UserName, credential.Password, countryId, product, fullChat.ToText(), cancellationToken)
			.ConfigureAwait(false);
		var conversation = new Conversation(
			new ConversationId(walletId, credential.UserName, credential.Password, orderId),
			fullChat,
			OrderStatus.Open,
			ConversationStatus.Started,
			new ConversationMetaData($"Order {GetNextConversationId(walletId)}"));
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

	public async Task AcceptOfferAsync(ConversationId conversationId, string firstName, string lastName, string address, string houseNumber, string zipCode, string city, string stateId, string countryName, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);

		var track = ConversationTracking.GetConversationTrackByd(conversationId);
		if (track.Conversation.ConversationStatus == ConversationStatus.InvoiceExpired)
		{
			throw new InvalidOperationException("Invoice has expired.");
		}

		string countryId = Countries.Single(c => c.Name == countryName).Id;

		await Client.SetBillingAddressAsync(track.Credential, firstName, lastName, address, houseNumber, zipCode, city, stateId, countryId, cancellationToken).ConfigureAwait(false);
		var newConversation = track.Conversation with
		{
			ChatMessages = track.Conversation.ChatMessages.AddSentMessage("Offer accepted")
		};
		await HandlePaymentAsync(track, newConversation, cancellationToken).ConfigureAwait(false);
	}

	private async Task HandlePaymentAsync(ConversationUpdateTrack track, Conversation newConversation,
		CancellationToken cancellationToken)
	{
		await Client.HandlePaymentAsync(track.Credential, track.Conversation.Id.OrderId, cancellationToken)
			.ConfigureAwait(false);
		track.Conversation = newConversation with
		{
			ConversationStatus = ConversationStatus.OfferAccepted
		};
		track.LastUpdate = DateTimeOffset.Now;

		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	// This method is used to mark conversations as read without sending requests to the webshop.
	// ChatMessage.IsUnread will arrive as false from the ViewModel, all we need to do is update the track and save to disk.
	public async Task UpdateConversationOnlyLocallyAsync(ConversationId conversationId, IEnumerable<ChatMessage> chatMessages, ConversationMetaData metaData, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		var track = ConversationTracking.GetConversationTrackByd(conversationId);

		track.Conversation = track.Conversation with
		{
			ChatMessages = new(chatMessages),
			MetaData = metaData,
		};

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
			if (order.CustomFields?.Concierge_Request_Status_State == "CLAIMED")
			{
				events |= ServerEvent.GenerateNewInvoice;
			}
			else if (order.CustomFields.PaidAfterExpiration)
			{
				events |= ServerEvent.ReceivePaymentAfterExpiration;
			}
			else
			{
				events |= ServerEvent.InvalidateInvoice;
			}
		}

		if (!string.IsNullOrWhiteSpace(order.CustomFields?.Btcpay_PaymentLink) && order.CustomFields?.BtcpayOrderStatus != "invoiceExpired")
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
		track.Conversation = updatedConversation;
		await UpdateConversationAsync(track.Conversation.Id, track.Conversation.ChatMessages, cancellationToken)
			.ConfigureAwait(false);
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

	private string[] GetLinksByLine(string attachmentsLinks) =>
		attachmentsLinks.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static string ConvertOfferDetailToMessages(Order order)
	{
		StringBuilder sb = new();
		sb.AppendLine("Our offer includes:");
		foreach (var lineItem in order.LineItems)
		{
			if (lineItem.Quantity > 1)
			{
				sb.AppendLine($"{lineItem.Quantity} {lineItem.Label} for ${lineItem.UnitPrice}/item (${lineItem.TotalPrice} total).");
			}
			else
			{
				sb.AppendLine($"A {lineItem.Label} for ${lineItem.UnitPrice}.");
			}
		}
		sb.AppendLine($"\nFor a total price of ${order.AmountTotal}.");
		return sb.ToString();
	}

	public async Task EnsureConversationsAreLoadedAsync(CancellationToken cancellationToken)
	{
		if (!IsConversationsLoaded)
		{
			await LoadConversationsAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task LoadConversationsAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureFileExists(FilePath);

		try
		{
			string json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
			var conversations = JsonConvert.DeserializeObject<ConversationTracking>(json) ?? new();
			ConversationTracking.Load(conversations);
		}
		catch (JsonException ex)
		{
			// Something happened with the file.
			var bakFilePath = $"{FilePath}.bak";
			Logger.LogError($"Wasabi was not able to load conversations file. Resetting the onversations and backup the corrupted file to: '{bakFilePath}'. Reason: '{ex}'.");
			File.Move(FilePath, bakFilePath, true);
			ConversationTracking.Load(new ConversationTracking());
			await SaveAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Wasabi was not able to load conversations file. Reason: '{ex}'.");
		}

		IsConversationsLoaded = true;
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
