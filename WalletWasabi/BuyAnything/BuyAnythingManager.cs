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

	// Todo: Is it ok that this is accessed without lock?
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
			// Check if the order state has changed and update the conversation status
			var orders = await Client
				.GetOrdersUpdateSinceAsync(track.Credential, cancel)
				.ConfigureAwait(false);

			// There is only one order per customer  and that's why we request all the orders
			// but with only expect to get one.
			var order = orders.Single();

			var customer = order.OrderCustomer;
			var fullConversation = customer.CustomFields.Wallet_Chat_Store;
			var messages = Parse(fullConversation).ToArray();
			if (messages.Length > track.Conversation.Messages.Length)
			{
				track.Conversation = track.Conversation with
				{
					Messages = messages.ToArray(),
				};
				ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(track.Conversation, customer.UpdatedAt ?? DateTimeOffset.Now));
			}

			// When the custom field in a Customer is updated, the order will not be updated, so this check kinda irrelevant.
			if (order.UpdatedAt.HasValue && order.UpdatedAt!.Value > track.LastUpdate)
			{
				// Update the conversation status according to the order state
				// TODO: Verify if the state machine is values match reality
				var status = order.StateMachineState.Name switch
				{
					"Cancelled" => ConversationStatus.Cancelled,
					"Done" => ConversationStatus.Finished,
					"In Progress" => ConversationStatus.WaitingForUpdates,
					_ => track.Conversation.Status
				};

				track.LastUpdate = order.UpdatedAt ?? DateTimeOffset.Now;
				track.Conversation = track.Conversation with
				{
					Status = status != track.Conversation.Status ? status : track.Conversation.Status
				};
				ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(track.Conversation, track.LastUpdate));
			}
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

	public async Task<Conversation> GetConversationsByIdAsync(ConversationId conversationId, CancellationToken cancellationToken)
	{
		await EnsureConversationsAreLoadedAsync(cancellationToken).ConfigureAwait(false);
		return Conversations
			.First(c => c.Conversation.Id == conversationId)
			.Conversation;
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
		await Client.CreateNewConversationAsync(credential.UserName, credential.Password, product, message, cancellationToken)
			.ConfigureAwait(false);

		Conversations.Add(new ConversationUpdateTrack(
			new Conversation(
				new ConversationId(walletId, credential.UserName, credential.Password),
				new[] { new ChatMessage(true, message) },
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
				Messages = track.Conversation.Messages.Append(new ChatMessage(true, newMessage)).ToArray(),
				Metadata = metadata,
				Status = ConversationStatus.WaitingForUpdates
			};
			track.LastUpdate = DateTimeOffset.Now;

			var rawText = ConvertToCustomerComment(track.Conversation.Messages);
			await Client.UpdateConversationAsync(track.Credential, rawText, cancellationToken).ConfigureAwait(false);

			await SaveAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public IEnumerable<ChatMessage> Parse(string customerComment)
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
			var text = EnsureProperRawMessage(items[1]);
			yield return new ChatMessage(isMine, text);
		}
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

	public string ConvertToCustomerComment(IEnumerable<ChatMessage> cleanChatMessages)
	{
		StringBuilder result = new();

		foreach (var chatMessage in cleanChatMessages)
		{
			var prefix = chatMessage.IsMyMessage ? "WASABI" : "SIB";
			result.Append($"||#{prefix}#{EnsureProperRawMessage(chatMessage.Message)}");
		}

		result.Append("||");

		return result.ToString();
	}

	private NetworkCredential GenerateRandomCredential() =>
		new(
			userName: $"{Guid.NewGuid()}@me.com",
			password: RandomString.AlphaNumeric(25));

	// Makes sure that the raw message doesn't contain characters that are used in the protocol. These chars are '#' and '||'.
	private string EnsureProperRawMessage(string message)
	{
		message = message.Replace("||", " ");
		message = message.Replace('#', '-');
		return message;
	}
}
