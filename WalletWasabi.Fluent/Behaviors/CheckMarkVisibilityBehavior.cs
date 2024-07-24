using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class CheckMarkVisibilityBehavior : AttachedToVisualTreeBehavior<PathIcon>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var ownerTextBox =
			AssociatedObject.FindAncestorOfType<TextBox>();

		if (ownerTextBox is null)
		{
			return;
		}

		var hasErrors = ownerTextBox.GetObservable(DataValidationErrors.HasErrorsProperty);
		var text = ownerTextBox.GetObservable(TextBox.TextProperty);

		hasErrors.ToSignal()
			.Merge(text.ToSignal())
			.Subscribe(_ =>
			{
				if (AssociatedObject is { })
				{
					AssociatedObject.IsVisible =
						!DataValidationErrors.GetHasErrors(ownerTextBox) &&
						!string.IsNullOrEmpty(ownerTextBox.Text);
				}
			})
			.DisposeWith(disposable);
	}
}
