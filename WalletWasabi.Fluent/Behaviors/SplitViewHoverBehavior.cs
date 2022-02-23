using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.BitcoinCore.Rpc.Models;

namespace WalletWasabi.Fluent.Behaviors;

public class SplitViewHoverBehavior : DisposingBehavior<SplitView>
{
	public static readonly StyledProperty<Action> ToggleActionProperty =
		AvaloniaProperty.Register<SplitViewHoverBehavior, Action>(nameof(ToggleAction));

	public static readonly StyledProperty<double> OpenPaneLengthProperty =
		AvaloniaProperty.Register<SplitViewHoverBehavior, double>(nameof(OpenPaneLength));

	public Action ToggleAction
	{
		get => GetValue(ToggleActionProperty);
		set => SetValue(ToggleActionProperty, value);
	}

	public double OpenPaneLength
	{
		get => GetValue(OpenPaneLengthProperty);
		set => SetValue(OpenPaneLengthProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}


		Observable
			.FromEventPattern<PointerEventArgs>(AssociatedObject, nameof(InputElement.PointerMoved))
			.Select(x => x.EventArgs.GetPosition(AssociatedObject))
			.Select(x => (x.X <= 100))
			.DistinctUntilChanged()
			.Subscribe(x =>
			{
				var navState = AssociatedObject.IsPaneOpen;

				if (!navState && x)
				{
					AssociatedObject.IsPaneOpen = true;
				}
			})
			.DisposeWith(disposables);
	}
}