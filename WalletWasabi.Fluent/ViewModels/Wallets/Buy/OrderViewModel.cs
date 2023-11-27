using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public partial class OrderViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<MessageViewModel> _messages;
	private readonly SourceList<MessageViewModel> _messagesList;

	[AutoNotify] private string? _message;

	public OrderViewModel(Guid id)
	{
		Id = id;

		_messagesList = new SourceList<MessageViewModel>();

		_messagesList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		var canSend = this.WhenAnyValue(x => x.Message)
			.Select(x => !string.IsNullOrWhiteSpace(x));

		SendCommand = ReactiveCommand.Create<string>(Send, canSend);

		Demo();
	}

	public Guid Id { get; }

	public required string Title { get; init; }

	public IReadOnlyCollection<MessageViewModel> Messages => _messages;

	public ICommand SendCommand { get; set; }

	private void Send(string message)
	{
		_messagesList.Edit(x =>
		{
			x.Add(
				new UserMessageViewModel
				{
					Message = message
				});
		});
	}

	private void Demo()
	{
		_messagesList.Edit(x =>
		{
			x.AddRange(
				new MessageViewModel[]
				{
					new UserMessageViewModel
					{
						Message = "I want my Lambo ASAP"
					},
					new AssistantMessageViewModel
					{
						Message = "OK, which color do you like it more?"
					},
					new UserMessageViewModel
					{
						Message = "Wasabi colors is right"
					},
					new AssistantMessageViewModel
					{
						Message = "Cool. Your Lamborguini Aventador is about to arrive. Be ready to open your garage's door."
					}
				});
		});
	}
}
