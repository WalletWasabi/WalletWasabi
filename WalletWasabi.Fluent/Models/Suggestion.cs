using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Models;

public class Suggestion
{
	public Suggestion(string text, Action onAccept)
	{
		Text = text;
		AcceptCommand = ReactiveCommand.Create(onAccept);
	}

	public string Text { get; }

	public ICommand AcceptCommand { get; set; }
}
