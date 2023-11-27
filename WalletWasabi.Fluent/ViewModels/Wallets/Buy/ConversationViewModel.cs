using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReactiveUI;
#pragma warning disable CA2000

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public partial class ConversationViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<Conversation> _conversations;
	[AutoNotify] private Conversation? _currentConversation;

	public ConversationViewModel()
	{
		var conversationCache = new SourceCache<Conversation, Guid>(x => x.Id);
		conversationCache
			.Connect()
			.Bind(out _conversations)
			.Subscribe();

		var conversations = new[]
		{
			new Conversation(Guid.NewGuid())
			{
				Title = "Order 001",
			},
			new Conversation(Guid.NewGuid())
			{
				Title = "Order 001",
			},
			new Conversation(Guid.NewGuid())
			{
				Title = "Order 001",
			}
		};

		conversationCache.AddOrUpdate(conversations);

		CurrentConversation = conversations.FirstOrDefault();
	}

	public ReadOnlyObservableCollection<Conversation> Conversations => _conversations;
}
