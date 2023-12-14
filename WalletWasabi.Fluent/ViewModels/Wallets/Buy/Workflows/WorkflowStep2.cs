using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowStep2
{
	public abstract Task<Conversation2> ExecuteAsync(Conversation2 conversation);
}

/// <summary>
/// Unifies the WorkflowStep class and the InputValidator class in a simple design
/// Supports adding bot messages to the conversation and then waiting for user input
/// </summary>
/// <typeparam name="TValue">The type of the Value input by the user</typeparam>
/// <remarks>this Class works in a stateless fashion. ExecuteAsync() receives a Conversation and returns the modified version of that same conversation, including bot messages, user messages, and metadata.</remarks>
public abstract partial class WorkflowStep2<TValue> : ReactiveObject, IWorkflowStep2
{
	private readonly TaskCompletionSource _userInputTcs = new();

	[AutoNotify] private string _caption = "Send";
	[AutoNotify] private TValue? _value;
	[AutoNotify] private bool _isValid;

	public WorkflowStep2(Conversation2 conversation)
	{
		// Retrieve initial value from the conversation. If the conversation was already stored it might contain a value for this Step, and thus this Step will not wait for user input.
		_value = RetrieveValue(conversation);

		// if this step already contains data previously stored in the Conversation (retrieved by RetrieveData),
		// then set the Step as completed so the parent workflow can move on.
		if (ValidateInitialValue(_value))
		{
			SetCompleted();
		}

		// When Value changes, call IsValidValue(Value) and set IsValid property accordingly.
		this.WhenAnyValue(x => x.Value)
			.Select(ValidateUserValue)
			.BindTo(this, x => x.IsValid);

		SendCommand = ReactiveCommand.Create(Send, this.WhenAnyValue(x => x.IsValid));
	}

	public ICommand SendCommand { get; }

	public Func<Conversation2, Task<Conversation2>>? OnCompleted { get; init; }

	private string StepName => GetType().Name;

	public virtual async Task<Conversation2> ExecuteAsync(Conversation2 conversation)
	{
		// Only ask the question if it hasn't been asked before
		if (!conversation.ChatMessages.Any(x => x.StepName == StepName && x.Source == MessageSource.Bot))
		{
			var botMessages = BotMessages(conversation).ToArray();

			foreach (var message in botMessages)
			{
				conversation = AddBotMessage(conversation, message);
			}
		}

		// Wait for user confirmation (Send button)
		await _userInputTcs.Task;

		if (Value is { } value)
		{
			// Update the Conversation Metadata with the current user-input value
			conversation = PutValue(conversation, value);

			if (StringValue(value) is { } userMessage)
			{
				// Update the Conversation and add a User Message with the current user-input value represented as text (if any)
				conversation = conversation with { ChatMessages = conversation.ChatMessages.AddUserMessage(userMessage, StepName) };
			}
		}

		if (OnCompleted is { })
		{
			conversation = await OnCompleted(conversation);
		}

		return conversation;
	}

	/// <summary>
	/// the question[s] the bot asks to the user before waiting for their input.
	/// </summary>
	protected virtual IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield break;
	}

	protected Conversation2 AddBotMessage(Conversation2 conversation, string message) =>
		conversation with { ChatMessages = conversation.ChatMessages.AddBotMessage(message, StepName) };

	protected void SetCompleted() => _userInputTcs.SetResult();

	/// <summary>
	/// Validate the Step's Value entered by the User. Fires when Value changes and is used to set IsValid property and the CanExecute of the SendCommand
	/// </summary>
	protected virtual bool ValidateUserValue(TValue? value) => value is { };

	/// <summary>
	/// Validate the Step's initial value retrieved from an existing Conversation.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	protected virtual bool ValidateInitialValue(TValue? value) => value is { };

	/// <summary>
	/// Retrieve the Step's Value from the Conversation Metadata
	/// </summary>
	protected abstract TValue? RetrieveValue(Conversation2 conversation);

	/// <summary>
	/// Add the Step's value into the Conversation Metadata
	/// </summary>
	protected abstract Conversation2 PutValue(Conversation2 conversation, TValue value);

	/// <summary>
	/// String representation of the Step's value (to show it as a User message)
	/// </summary>
	protected virtual string? StringValue(TValue value) => null;

	/// <summary>
	/// This is called when the user presses the "Send" button. It simply signals the TaskCompletionSource so that ExecuteAsync() can move on.
	/// </summary>
	private void Send()
	{
		if (IsValid)
		{
			SetCompleted();
		}
	}
}
