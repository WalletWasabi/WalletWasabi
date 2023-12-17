using ReactiveUI;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class UserMessageViewModel : MessageViewModel
{
	public UserMessageViewModel(Workflow workflow, ChatMessage message) : base(message)
	{
		Workflow = workflow;

		var canEdit =
			this.WhenAnyValue(x => x.Workflow.Conversation)
				.Select(_ => Workflow.MessageEditor.IsEditable(message));

		EditCommand = ReactiveCommand.CreateFromTask(() => EditAsync(message), canEdit);
	}

	public ICommand EditCommand { get; }

	// used just to be able to use this.WhenAnyValue()
	private Workflow Workflow { get; }

	private async Task EditAsync(ChatMessage message)
	{
		var editor = Workflow.MessageEditor.Get(message);

		// TODO: navigate to edit dialog, (**** OR EVEN BETTER, ENABLE IN PLACE EDITION IN THE CHAT WINDOW ****) show editor (requires datatemplate)

		// TODO: if editor is null do not crash
		var newMessage = await editor.EditMessageAsync(message);

		// TODO:
		//Workflow.SetConversation(conversation);

		// TODO: refresh viewmodel values from new message
	}
}
