using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.Custom;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class WhitespaceInputRemovalBehavior : DisposingBehavior<TextBox>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject != null)
		{
			AssociatedObject.OnEvent(InputElement.TextInputEvent, RoutingStrategies.Tunnel)
				.Select(x => x.EventArgs)
				.Do(x => Filter(x, x.Text))
				.Subscribe();
		}
	}

	private void Filter(TextInputEventArgs textInputEventArgs, string? objText)
	{
		textInputEventArgs.Text = objText?.WithoutWhitespace();
	}
}
