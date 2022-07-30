using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors;

public class ListBoxItemPointerOverSelectedBehavior : DisposingBehavior<ListBoxItem>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		//var listBox = AssociatedObject.FindAncestorOfType<ListBox>();

		//if (listBox is null)
		//{
		//	return;
		//}

		Observable.FromEventPattern<AvaloniaPropertyChangedEventArgs>(AssociatedObject, nameof(PropertyChanged))
		.Select(x => x.EventArgs)
		.Subscribe(e =>
		{
			if (e.Property == InputElement.IsPointerOverProperty && e.NewValue is true)
			{
				AssociatedObject.IsSelected = true;
				//listBox.SelectedItem = AssociatedObject.DataContext;
			}
		})
		.DisposeWith(disposables);
	}
}
