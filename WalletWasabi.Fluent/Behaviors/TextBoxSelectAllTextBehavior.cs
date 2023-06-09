using System.Reactive.Disposables;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Behaviors;

internal class TextBoxSelectAllTextBehavior : AttachedToVisualTreeBehavior<TextBox>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		AssociatedObject?.SelectAll();
	}
}
