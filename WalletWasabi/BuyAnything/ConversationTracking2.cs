using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.BuyAnything;

public class ConversationTracking2
{
	public Dictionary<string, int> NextConversationIds { get; private set; } = new();
	public List<ConversationUpdateTrack2> Conversations { get; } = new();
	private readonly object _syncObj = new();

	public void Load(ConversationTracking2 conversations)
	{
		lock (_syncObj)
		{
			NextConversationIds = conversations.NextConversationIds;
			Conversations.AddRange(conversations.Conversations.Where(c => c is not null));
		}
	}

	public ConversationUpdateTrack2[] GetUpdatableConversations()
	{
		lock (_syncObj)
		{
			return Conversations.Where(c => c.Conversation.IsUpdatable()).ToArray();
		}
	}

	public Conversation2[] GetConversationsByWalletId(string walletId)
	{
		lock (_syncObj)
		{
			return Conversations
				.Where(c => c.Conversation.Id.WalletId == walletId)
				.Select(c => c.Conversation)
				.ToArray();
		}
	}

	public ConversationUpdateTrack2 GetConversationTrackByd(ConversationId conversationId)
	{
		lock (_syncObj)
		{
			return Conversations.First(c => c.Conversation.Id == conversationId);
		}
	}

	public Conversation2 GetConversationsById(ConversationId conversationId) =>
		GetConversationTrackByd(conversationId).Conversation;

	public void Add(ConversationUpdateTrack2 conversationUpdateTrack)
	{
		lock (_syncObj)
		{
			Conversations.Add(conversationUpdateTrack);
			var walletId = conversationUpdateTrack.Conversation.Id.WalletId;
			NextConversationIds[walletId] = NextConversationIds.TryGetValue(walletId, out var cid)
				? cid + 1
				: 1;
		}
	}

	public int RemoveAll(Predicate<ConversationUpdateTrack2> predicate)
	{
		lock (_syncObj)
		{
			return Conversations.RemoveAll(predicate);
		}
	}

	public int GetNextConversationId(string walletId)
	{
		lock (_syncObj)
		{
			int next = NextConversationIds.TryGetValue(walletId, out var cid)
				? cid
				: 1;
			NextConversationIds[walletId] = next;
			return next;
		}
	}
}
