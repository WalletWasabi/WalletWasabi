using ReactiveUI;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class UserMessageViewModel : MessageViewModel
{
	[AutoNotify] private bool _isEditing;

	public UserMessageViewModel(Workflow workflow, ChatMessage message, IObservable<bool> isEditing) : base(message)
	{
		Workflow = workflow;

		var messageIsEditable = this.WhenAnyValue(x => x.Workflow.Conversation)
			.Select(_ => Workflow.MessageEditor.IsEditable(message));
		var canEdit = isEditing.CombineLatest(messageIsEditable, (isEd, canE) => !isEd && canE);
		IsEditable = canEdit;
		var editCommand = ReactiveCommand.CreateFromTask(() => EditAsync(message), canEdit);
		editCommand.IsExecuting.BindTo(this, x => x.IsEditing);
		EditCommand = editCommand;
	}

	public IObservable<bool> IsEditable { get; }

	public ICommand EditCommand { get; }

	// used just to be able to use this.WhenAnyValue()
	private Workflow Workflow { get; }

	private async Task EditAsync(ChatMessage message)
	{
		var editor = Workflow.MessageEditor.Get(message);

		if (editor is null)
		{
			return;
		}

		var currentStep = Workflow.CurrentStep;
		Workflow.CurrentStep = editor;

		var newMessage = await editor.EditMessageAsync(message);

		Message = newMessage;
		IsUnread = newMessage.IsUnread;
		OriginalText = newMessage.Text;
		UiMessage = newMessage.Text;

		Workflow.CurrentStep = currentStep;
		Workflow.Conversation = editor.Conversation;
	}
}
