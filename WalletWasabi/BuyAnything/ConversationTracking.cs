using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.BuyAnything;

public class ConversationTracking
{
	public Dictionary<string,int> NextConversationIds { get; private set; } = new();
	public List<ConversationUpdateTrack> Conversations { get; } = new();

	public void Load(ConversationTracking conversations)
	{
		NextConversationIds = conversations.NextConversationIds;
		Conversations.AddRange(conversations.Conversations.Where(c => c is not null));
	}

	public IEnumerable<ConversationUpdateTrack> UpdatableConversations =>
		Conversations.Where(c => c.Conversation.IsUpdatable());

	public Conversation[] GetConversationsByWalletId(string walletId) =>
		Conversations
			.Where(c => c.Conversation.Id.WalletId == walletId)
			.Select(c => c.Conversation)
			.ToArray();

	public ConversationUpdateTrack GetConversationTrackByd(ConversationId conversationId) =>
		Conversations
			.First(c => c.Conversation.Id == conversationId);

	public Conversation GetConversationsById(ConversationId conversationId) =>
		GetConversationTrackByd(conversationId).Conversation;

	public void Add(ConversationUpdateTrack conversationUpdateTrack)
	{
		Conversations.Add(conversationUpdateTrack);
		var walletId = conversationUpdateTrack.Conversation.Id.WalletId;
		NextConversationIds[walletId] = NextConversationIds.TryGetValue(walletId, out var cid)
			? cid + 1
			: 1;
	}

	public int RemoveAll(Predicate<ConversationUpdateTrack> predicate) =>
		Conversations.RemoveAll(predicate);

	public int GetNextConversationId(string walletId) =>
		NextConversationIds[walletId] = NextConversationIds.TryGetValue(walletId, out var cid)
			? cid
			: 1;
}
