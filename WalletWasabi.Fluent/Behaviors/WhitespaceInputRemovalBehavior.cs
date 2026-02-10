using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class WhitespaceInputRemovalBehavior : DisposingBehavior<TextBox>
{
	protected override IDisposable OnAttachedOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		return AssociatedObject.OnEvent(InputElement.TextInputEvent, RoutingStrategies.Tunnel)
			.Select(x => x.EventArgs)
			.Do(x => Filter(x, x.Text))
			.Subscribe();
	}

	private static void Filter(TextInputEventArgs textInputEventArgs, string? objText)
	{
		textInputEventArgs.Text = objText?.WithoutWhitespace();
	}
}
