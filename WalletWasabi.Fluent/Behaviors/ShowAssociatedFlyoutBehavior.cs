using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowAssociatedFlyoutBehavior : Behavior<Control>
{
	public static readonly StyledProperty<bool> IsSearchPanelOpenProperty =
		AvaloniaProperty.Register<ShowAssociatedFlyoutBehavior, bool>(nameof(IsSearchPanelOpen));

	private IDisposable? _isOpenSyncer;
	private IDisposable? _flyoutToggler;

	public bool IsSearchPanelOpen
	{
		get => GetValue(IsSearchPanelOpenProperty);
		set => SetValue(IsSearchPanelOpenProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		_isOpenSyncer = FlyoutBase.GetAttachedFlyout(AssociatedObject)
			.GetObservable(FlyoutBase.IsOpenProperty)
			.Subscribe(b => IsSearchPanelOpen = b);

		_flyoutToggler = this.GetObservable(IsSearchPanelOpenProperty).Subscribe(isOpen =>
		{
			if (isOpen)
			{
				FlyoutBase.ShowAttachedFlyout(AssociatedObject);
			}
			else
			{
				FlyoutBase.GetAttachedFlyout(AssociatedObject).Hide();
			}
		});
	}

	protected override void OnDetachedFromVisualTree()
	{
		_flyoutToggler?.Dispose();
		_flyoutToggler?.Dispose();

		base.OnDetachedFromVisualTree();
	}
}