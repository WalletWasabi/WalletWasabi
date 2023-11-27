using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public class Conversation
{
	private readonly ReadOnlyObservableCollection<Message> _messages;

	public Conversation(Guid id)
	{
		Id = id;

		var msgList = new SourceList<Message>();
		msgList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		msgList.AddRange(
			new Message[]
			{
				new("I want my Lambo ASAP", SenderKind.User),
				new("OK, which color do you like it more?", SenderKind.Backend),
				new("Wasabi colors is right", SenderKind.User),
				new("Cool. Your Lamborguini Aventador is about to arrive. Be ready to open your garage's door.", SenderKind.Backend),
			});
	}

	public Guid Id { get; }
	public required string Title { get; init; }
	public IReadOnlyCollection<Message> Messages => _messages;
}
