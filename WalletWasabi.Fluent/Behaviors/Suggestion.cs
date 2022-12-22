using System.Reactive;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class Suggestion : ReactiveObject
{
	public Suggestion(string text, Action onAccept)
	{
		Text = text;
		AcceptCommand = ReactiveCommand.Create(onAccept);
	}

	public string Text { get; }

	public ReactiveCommand<Unit, Unit> AcceptCommand { get; set; }
}
