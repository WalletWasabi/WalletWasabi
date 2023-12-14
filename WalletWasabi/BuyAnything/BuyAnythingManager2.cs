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

// Class to manage the conversation updates
public class BuyAnythingManager2 : PeriodicRunner
{
	public BuyAnythingManager2(string dataDir, TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
		FilePath = Path.Combine(dataDir, "Conversations", "Conversations.json");

		string countriesFilePath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "BuyAnything", "Data", "Countries.json");
		string fileContent = File.ReadAllText(countriesFilePath);
		Country[] countries = JsonConvert.DeserializeObject<Country[]>(fileContent)
							  ?? throw new InvalidOperationException("Couldn't read the countries list.");

		Countries = new List<Country>(countries);
	}

	private BuyAnythingClient Client { get; }
	public IReadOnlyList<Country> Countries { get; }

	private ConversationTracking2 ConversationTracking { get; } = new();

	private bool IsConversationsLoaded { get; set; }
	private string FilePath { get; }

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
	}

	public int GetNextConversationId(string walletId) =>
		ConversationTracking.GetNextConversationId(walletId);

	public async Task<Conversation2[]> GetConversationsAsync(string walletId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return ConversationTracking.GetConversationsByWalletId(walletId);
	}

	public async Task<Conversation2> GetConversationByIdAsync(ConversationId conversationId, CancellationToken cancellationToken)
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

	public async Task<Conversation2> StartNewConversationAsync(Wallet wallet, Conversation2 conversation, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);

		var country = conversation.MetaData.Country;
		var product = conversation.MetaData.Product;

		if (country is not { } || product is not { })
		{
			throw new ArgumentException("Conversation is missing Country or Product.");
		}

		var fullChat = new Chat2(conversation.ChatMessages);
		var credential = GenerateRandomCredential();
		var walletId = GetWalletId(wallet);

		var orderId =
			await Client.CreateNewConversationAsync(credential.UserName, credential.Password, country.Id, product.Value, fullChat.ToText(), cancellationToken)
						.ConfigureAwait(false);

		conversation = new Conversation2(
			new ConversationId(walletId, credential.UserName, credential.Password, orderId),
			fullChat,
			OrderStatus.Open,
			ConversationStatus.Started,
			conversation.MetaData with { Title = $"Order {GetNextConversationId(walletId)}" });

		ConversationTracking.Add(new ConversationUpdateTrack(conversation));

		//ConversationUpdated?.SafeInvoke(this, new(conversation, DateTimeOffset.Now));

		await SaveAsync(cancellationToken).ConfigureAwait(false);

		return conversation;
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
			else if (orderStatus == OrderStatus.Cancelled)
			{
				events |= ServerEvent.CloseCancelled;
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

	private Task SendSystemChatLinesAsync(ConversationUpdateTrack track, string message, DateTimeOffset? updatedAt,
		ConversationStatus newStatus, CancellationToken cancellationToken) =>
		SendSystemChatLinesAsync(track, message, DataCarrier.NoData, updatedAt, newStatus, cancellationToken);

	private async Task FinishConversationAsync(ConversationUpdateTrack track, CancellationToken cancellationToken)
	{
		var updatedConversation = track.Conversation.AddSystemChatLine("Conversation finished.", DataCarrier.NoData, ConversationStatus.Finished);
		track.Conversation = updatedConversation;
		ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(updatedConversation, DateTimeOffset.Now));
		await SaveAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		JsonSerializerSettings settings = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.All
		};

		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(ConversationTracking, Formatting.Indented, settings);
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
			JsonSerializerSettings settings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.All
			};

			string json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
			var conversations = JsonConvert.DeserializeObject<ConversationTracking2>(json, settings) ?? new();
			ConversationTracking.Load(conversations);
		}
		catch (JsonException ex)
		{
			// Something happened with the file.
			var bakFilePath = $"{FilePath}.bak";
			Logger.LogError($"Wasabi was not able to load conversations file. Resetting the onversations and backup the corrupted file to: '{bakFilePath}'. Reason: '{ex}'.");
			File.Move(FilePath, bakFilePath, true);
			ConversationTracking.Load(new ConversationTracking2());
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
}
