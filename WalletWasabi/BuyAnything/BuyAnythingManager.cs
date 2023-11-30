
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.BuyAnything;


// Event that is raised when we detect an update in the server
public record ConversationUpdateEvent(Conversation Conversation, DateTimeOffset LastUpdate);

public record ChatMessage(bool IsMyMessage, string Message);

// Class to keep a track of the last update of a conversation

// Class to manage the conversation updates
public class BuyAnythingManager : PeriodicRunner
{

	private static readonly string CountriesPath = "./Data/Countries.json";

	public BuyAnythingManager(string dataDir, TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
		FilePath = Path.Combine(dataDir, "Conversations", "Conversations.json");
	}

	private static Dictionary<string, string> Countries { get; set; } = new();
	private BuyAnythingClient Client { get; }

	// Todo: Is it ok that this is accessed without lock?
	private List<ConversationUpdateTrack> Conversations { get; } = new ();
	private bool IsConversationsLoaded { get; set; }

	private string FilePath { get; }

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Load the conversations from the disk in case they were not loaded yet
		await EnsureConversationsAreLoadedAsync(cancel).ConfigureAwait(false);

		// Iterate over the conversations that are updatable
		var conversationsToUpdate = Conversations.Where(c => c.IsUpdatable).ToList();
		foreach (var track in conversationsToUpdate)
		{
			// Todo: This should always get back 0 or 1 result as each customer has only one conversation.
			var orders = await Client
				.GetConversationsUpdateSinceAsync(track.Conversation.Id.Customer, track.LastUpdate, cancel)
				.ConfigureAwait(false);

			foreach (var order in orders.Where(o => o.UpdatedAt.HasValue && o.UpdatedAt!.Value > track.LastUpdate))
			{
				var orderLastUpdated = order.UpdatedAt!.Value;

				// Update the conversation status according to the order state
				// TODO: Verify if the state machine is values match reality
				var status = order.StateMachineState.Name switch
				{
					"Cancelled" => ConversationStatus.Cancelled,
					"Completed" => ConversationStatus.Finished,
					"InProgress" => ConversationStatus.WaitingForUpdates,
					_ => track.Conversation.Status
				};

				// FIXME: this is not tested. The chat messages are now stored in a customers' profile custom field
				// while previously they were stored in the order. That means that we have to check the customer
				// profiles instead of checking the orders. However, given that the orders come with the customers'
				// profile, this could work.
				var newMessageFromConcierge = Parse(order.GetCustomerProfileComment());
				if (status != track.Conversation.Status)
				{
					Logger.LogInfo($"Status of order {order.Id} updated: {track.Conversation.Status} -> {status}");
				}

				var messages = Parse(order.GetCustomerProfileComment()).ToList();

				if (messages.Count > track.Conversation.Messages.Count)
				{
					Logger.LogInfo($"Order {order.Id} has {messages.Count - track.Conversation.Messages.Count} new messages");
				}
				track.LastUpdate = orderLastUpdated;
				track.Conversation = track.Conversation with
				{
					Messages = newMessageFromConcierge.ToList(),
					Status = status != track.Conversation.Status ? status : track.Conversation.Status
				};
				ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(track.Conversation, orderLastUpdated));
			}
		}
		Logger.LogDebug($"{conversationsToUpdate.Count} conversations updated for all wallets.");
	}

	public async Task<Conversation[]> GetConversationsAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		var walletId = GetWalletId(wallet);
		var result = Conversations
			.Where(c => c.Conversation.Id.WalletId == walletId)
			.Select(c => c.Conversation)
			.ToArray();

		Logger.LogDebug($"Wallet {walletId} has {result.Length} conversations");

		return result;
	}

	public async Task StartNewConversationAsync(string walletId, string countryId, BuyAnythingClient.Product product, string message, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		var newConversation =  await Client.CreateNewConversationAsync(countryId, product, message, cancellationToken)
			.ConfigureAwait(false);

		Conversations.Add(new ConversationUpdateTrack(
			new Conversation(
				new ConversationId(walletId, newConversation.OrderNumber, newConversation.Customer),
				new List<ChatMessage> { new (true, message) },
				ConversationStatus.Started,
				new object())));

		await SaveAsync(cancellationToken).ConfigureAwait(false);

		Logger.LogInfo($"Conversation {newConversation.OrderNumber} was created.");
	}

	public async Task UpdateConversationAsync(ConversationId conversationId, string newMessage, object metadata, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		if (Conversations.FirstOrDefault(c => c.Conversation.Id == conversationId) is { } track)
		{
			track.Conversation = track.Conversation with
			{
				Messages = track.Conversation.Messages.Append(new ChatMessage(false, newMessage)).ToList(),
				Metadata = metadata,
				Status = ConversationStatus.WaitingForUpdates
			};
			track.LastUpdate = DateTimeOffset.Now;

			var rawText = ConvertToCustomerComment(track.Conversation.Messages);
			await Client.UpdateConversationAsync(track.Conversation.Id.Customer, rawText).ConfigureAwait(false);

			await SaveAsync(cancellationToken).ConfigureAwait(false);

			Logger.LogDebug($"Conversation {conversationId} was updated.");
		}
	}

	private IEnumerable<ChatMessage> Parse(string customerComment)
	{
		var messages = customerComment.Split("||", StringSplitOptions.RemoveEmptyEntries);

		foreach (var message in messages)
		{
			var items = message.Split("#", StringSplitOptions.RemoveEmptyEntries);

			if (items.Length != 2)
			{
				yield break;
			}

			var isMine = items[0] == "WASABI";
			var text = items[1];
			yield return new ChatMessage(isMine, text);
		}
	}

	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(Conversations, Formatting.Indented);
		await File.WriteAllTextAsync(FilePath, json, cancellationToken).ConfigureAwait(false);
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

	private static string ConvertToCustomerComment(IEnumerable<ChatMessage> cleanChatMessages)
	{
		StringBuilder result = new();

		foreach (var chatMessage in cleanChatMessages)
		{
			var prefix = chatMessage.IsMyMessage ? "WASABI" : "SIB";
			result.Append($"||#{prefix}#{chatMessage.Message}");
		}

		result.Append("||");

		return result.ToString();
	}

	public static string GetWalletId (Wallet wallet) =>
		(wallet.KeyManager.MasterFingerprint is { } masterFingerprint
			? masterFingerprint.ToString()
			: "readonly wallet")
		?? string.Empty;

	private async Task<Dictionary<string, string>> GetCountryListAsync(CancellationToken cancellationToken)
	{
		if (Countries.Any())
		{
			return Countries;
		}

		Countries = JsonConvert.DeserializeObject<Dictionary<string, string>>(
			await File.ReadAllTextAsync(CountriesPath, cancellationToken).ConfigureAwait(false)) ?? throw new InvalidOperationException("Couldn't read cached countries values.");

		return Countries;
	}
}
