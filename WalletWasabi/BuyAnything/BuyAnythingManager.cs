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
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.BuyAnything;

// Event that is raised when we detect an update in the server
public record ConversationUpdateEvent(Conversation Conversation, DateTimeOffset LastUpdate);

public record ChatMessage(bool IsMyMessage, string Message);

public record Country(string Id, string Name);

// Class to manage the conversation updates
public class BuyAnythingManager : PeriodicRunner
{
	public BuyAnythingManager(string dataDir, TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
		FilePath = Path.Combine(dataDir, "Conversations", "Conversations.json");
	}

	private BuyAnythingClient Client { get; }
	private static List<Country> Countries { get; } = new();

	// Todo: Is it ok that this is accessed without lock? It feels risky
	private List<ConversationUpdateTrack> Conversations { get; } = new();

	private bool IsConversationsLoaded { get; set; }

	private string FilePath { get; }

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Load the conversations from the disk in case they were not loaded yet
		await EnsureConversationsAreLoadedAsync(cancel).ConfigureAwait(false);

		// Iterate over the conversations that are updatable
		foreach (var track in Conversations.Where(c => c.Conversation.IsUpdatable()))
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
		// but with only expect to get one.
		var order = orders.Single();
		var orderCustomFields = order.CustomFields;

		// When the custom field in a Customer is updated, the order will not be updated, so this check kinda irrelevant.
		// FIXME: is this if really necessary????
		if (order.UpdatedAt.HasValue && order.UpdatedAt!.Value > track.LastUpdate)
		{
			// Update the conversation status according to the order state
			var orderStatus = GetOrderStatus(order);

			switch (track.Conversation.ConversationStatus)
			{
				// This means that in "lineItems" we have the offer data
				case ConversationStatus.Started
					when orderCustomFields.Concierge_Request_Status_State == "OFFER":
					track.Conversation = await SendSystemChatLinesAsync(track.Conversation,
						ConvertOfferDetailToMessages(order),
						order.UpdatedAt, ConversationStatus.OfferReceived, cancel).ConfigureAwait(false);
					break;

				// Once the user accepts the offer, the system generates a bitcoin address and amount
				case ConversationStatus.OfferAccepted:
				{
					var attachedLink = orderCustomFields.Concierge_Request_Attachements_Links;
					var offerMessages = new List<string>();
					if (!string.IsNullOrWhiteSpace(attachedLink))
					{
						offerMessages.Add($"Check the attached file: {attachedLink}");
					}

					var bip21 = orderCustomFields.Btcpay_PaymentLink;

					offerMessages.Add($"Pay to: {bip21}. The invoice expires in 10 minutes.");

					track.Conversation = await SendSystemChatLinesAsync(track.Conversation, offerMessages,
						order.UpdatedAt,
						ConversationStatus.InvoiceReceived, cancel).ConfigureAwait(false);
					break;
				}

				// The status changes to "In Progress" after the user paid
				case ConversationStatus.InvoiceReceived
					when orderStatus == OrderStatus.InProgress && track.Conversation.OrderStatus == OrderStatus.Open:
					track.Conversation = await SendSystemChatLinesAsync(track.Conversation, new[] {"Payment confirmed"},
						order.UpdatedAt, ConversationStatus.PaymentConfirmed, cancel).ConfigureAwait(false);
					break;

				// In case the invoice expires we communicate this fact to the chat
				case ConversationStatus.InvoiceReceived
					when orderCustomFields.BtcpayOrderStatus == "invoiceExpired":
					track.Conversation = await SendSystemChatLinesAsync(track.Conversation, new[] {"Invoice has expired"},
						order.UpdatedAt, ConversationStatus.PaymentConfirmed, cancel).ConfigureAwait(false);
					break;

			}
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

	public async Task<Conversation[]> GetConversationsAsync(string walletId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return Conversations
			.Where(c => c.Conversation.Id.WalletId == walletId)
			.Select(c => c.Conversation)
			.ToArray();
	}

	public async Task<Conversation> GetConversationByIdAsync(ConversationId conversationId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return Conversations
			.First(c => c.Conversation.Id == conversationId)
			.Conversation;
	}

	public async Task<int> RemoveConversationsByIdsAsync(IEnumerable<ConversationId> toRemoveIds, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		var removedCount = Conversations.RemoveAll(x => toRemoveIds.Contains(x.Conversation.Id));
		if (removedCount > 0)
		{
			await SaveAsync(cancellationToken).ConfigureAwait(false);
		}

		return removedCount;
	}

	public async Task<Country[]> GetCountriesAsync(CancellationToken cancellationToken)
	{
		await EnsureCountriesAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return Countries.ToArray();
	}

	public async Task StartNewConversationAsync(string walletId, BuyAnythingClient.Product product, string message, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);

		var credential = GenerateRandomCredential();
		var orderId = await Client.CreateNewConversationAsync(credential.UserName, credential.Password, product, message, cancellationToken)
			.ConfigureAwait(false);

		Conversations.Add(new ConversationUpdateTrack(
			new Conversation(
				new ConversationId(walletId, credential.UserName, credential.Password, orderId),
				new Chat(new[] { new ChatMessage(true, message) }),
				OrderStatus.Open,
				ConversationStatus.Started,
				new object())));

		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task UpdateConversationAsync(ConversationId conversationId, string newMessage, object metadata, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		if (Conversations.FirstOrDefault(c => c.Conversation.Id == conversationId) is { } track)
		{
			track.Conversation = track.Conversation with
			{
				ChatMessages = track.Conversation.ChatMessages.AddSentMessage(newMessage),
				Metadata = metadata,
			};
			track.LastUpdate = DateTimeOffset.Now;

			var rawText = track.Conversation.ChatMessages.ToText();
			await Client.UpdateConversationAsync(track.Credential, rawText, cancellationToken).ConfigureAwait(false);

			await SaveAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task AcceptOfferAsync(ConversationId conversationId, string firstName, string lastName, string address, string houseNumber, string zipCode, string city, string countryId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);

		if (Conversations.FirstOrDefault(track => track.Conversation.Id == conversationId) is { } track)
		{
			await Client.SetBillingAddressAsync(track.Credential, firstName, lastName, address, houseNumber, zipCode, city, countryId, cancellationToken).ConfigureAwait(false);
			await Client.HandlePaymentAsync(track.Credential, track.Conversation.Id.OrderId, cancellationToken).ConfigureAwait(false);
			track.Conversation = track.Conversation with
			{
				ChatMessages = track.Conversation.ChatMessages.AddSentMessage("Offer accepted"),
				ConversationStatus = ConversationStatus.OfferAccepted
			};
			track.LastUpdate = DateTimeOffset.Now;
		}
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

	private async Task<Conversation> SendSystemChatLinesAsync(Conversation conversation, IEnumerable<string> messages, DateTimeOffset? updatedAt, ConversationStatus newStatus, CancellationToken cancellationToken)
	{
		var updatedConversation = conversation.AddSystemChatLines(messages, newStatus);
		ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(updatedConversation, updatedAt ?? DateTimeOffset.Now));
		await SaveAsync(cancellationToken).ConfigureAwait(false);
		return updatedConversation;
	}
	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(Conversations, Formatting.Indented);
		await File.WriteAllTextAsync(FilePath, json, cancellationToken).ConfigureAwait(false);
	}

	public static string GetWalletId(Wallet wallet) =>
		wallet.KeyManager.MasterFingerprint is { } masterFingerprint
			? masterFingerprint.ToString()
			: "readonly wallet";

	private static IEnumerable<string> ConvertOfferDetailToMessages(Order order)
	{
		foreach (var lineItem in order.LineItems)
		{
			yield return $"{lineItem.Quantity} x {lineItem.Label} ---unit price: {lineItem.UnitPrice} ---total price: {lineItem.TotalPrice}";
		}
	}

	private async Task EnsureConversationsAreLoadedAsync(CancellationToken cancellationToken)
	{
		if (IsConversationsLoaded is false)
		{
			await LoadConversationsAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task LoadConversationsAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureFileExists(FilePath);
		string json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
		var conversations = JsonConvert.DeserializeObject<List<ConversationUpdateTrack>>(json) ?? new();
		Conversations.AddRange(conversations);
		IsConversationsLoaded = true;
	}

	private static async Task EnsureCountriesAreLoadedAsync(CancellationToken cancellationToken)
	{
		if (Countries.Count == 0)
		{
			await LoadCountriesAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private static async Task LoadCountriesAsync(CancellationToken cancellationToken)
	{
		var countriesFilePath = "./BuyAnything/Data/Countries.json";
		var fileContent = await File.ReadAllTextAsync(countriesFilePath, cancellationToken).ConfigureAwait(false);

		var countries = JsonConvert.DeserializeObject<Country[]>(fileContent)
						?? throw new InvalidOperationException("Couldn't read cached countries values.");

		Countries.AddRange(countries);
	}

	private NetworkCredential GenerateRandomCredential() =>
		new(
			userName: $"{Guid.NewGuid()}@me.com",
			password: RandomString.AlphaNumeric(25));

}
