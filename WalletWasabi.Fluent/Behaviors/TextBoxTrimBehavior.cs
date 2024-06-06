using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class TextBoxTrimBehavior : Behavior<TextBox>
{
	protected override void OnAttached()
	{
		base.OnAttached();
		
		AssociatedObject.OnEvent(InputElement.TextInputEvent, RoutingStrategies.Tunnel)
			.Select(x => x.EventArgs)
			.Do(x => Filter(x, AssociatedObject, x.Text))
			.Subscribe();
	}

	private void Filter(TextInputEventArgs textInputEventArgs, TextBox associatedObject, string? objText)
	{
		textInputEventArgs.Text = objText?.Replace(" ", "");
	}
}
